// The protocol with Erapkey over any message transport: the native messaging port in the browser,
// the named pipe in the Node test (docs/browser-extension.md#pairing, #session, #requests).
import {
  NONCE_SIZE, deriveSessionKeys, exportPublicKey, fromBase64, generateKeyPair, importPublicKey, open,
  pairingCode, randomBytes, seal, toBase64, type SessionKeys,
} from './crypto';

export const PROTOCOL_VERSION = 1;

/** Error codes of the protocol, plus the ones of this side. */
export type ErrorCode =
  | 'locked' | 'not_paired' | 'not_found' | 'bad_request' | 'unsupported_version' | 'internal' | 'denied'
  | 'app_not_running' | 'disconnected' | 'timeout';

export class HostError extends Error {
  constructor(public readonly code: ErrorCode) {
    super(code);
  }
}

export interface Transport {
  send(message: unknown): void;
  close(): void;
}

/** What the extension keeps about its pairing. The private key is a non-extractable CryptoKey. */
export interface Pairing {
  keys: CryptoKeyPair;
  pairingId?: string;
  /** The static public key Erapkey made for this pairing (SPKI, base64). */
  appPublicKey?: string;
}

export interface Login {
  entryId: string;
  name: string;
  login: string;
  match: 'exact' | 'domain';
}

export interface Credentials {
  login: string;
  password: string;
  totp?: string;
}

interface Waiter {
  resolve: (message: any) => void;
  reject: (error: HostError) => void;
}

const PLAIN_TIMEOUT = 10_000;

export class Client {
  private session?: SessionKeys;
  private sendCounter = 0;
  private receiveCounter = 0;
  private nextId = 1;
  private readonly pending = new Map<number, Waiter>();
  private readonly plainWaiters: Waiter[] = [];
  private readonly plainMessages: any[] = [];
  private sendChain: Promise<void> = Promise.resolve();
  private receiveChain: Promise<void> = Promise.resolve();
  private closedWith?: ErrorCode;
  private deriving = false;
  private readonly early: any[] = [];

  /** Events pushed by Erapkey: locked, unlocked, vaultChanged. */
  onEvent?: (name: string) => void;
  /** Called once when the connection ends. */
  onClose?: (code: ErrorCode) => void;

  constructor(private readonly transport: Transport) {}

  get isOpen(): boolean {
    return this.closedWith === undefined;
  }

  get hasSession(): boolean {
    return this.session !== undefined && this.isOpen;
  }

  /** Feed every message of the transport here, in order. */
  receive(message: any): void {
    if (!this.isOpen) return;
    if (!this.session) {
      if (message?.event === 'app_not_running') {
        this.close('app_not_running');
        return;
      }
      // Erapkey may send an event right after its hello answer, while the keys are still being derived here.
      if (this.deriving && typeof message?.c === 'string') {
        this.early.push(message);
        return;
      }
      const waiter = this.plainWaiters.shift();
      if (waiter) waiter.resolve(message);
      else this.plainMessages.push(message);
      return;
    }
    // Decryption is asynchronous; the chain keeps the counters in order.
    this.receiveChain = this.receiveChain.then(() => this.receiveEncrypted(message)).catch(() => this.close('bad_request'));
  }

  /** The transport went away. */
  close(code: ErrorCode = 'disconnected'): void {
    if (!this.isOpen) return;
    this.closedWith = code;
    const error = new HostError(code);
    for (const waiter of this.plainWaiters.splice(0)) waiter.reject(error);
    for (const waiter of this.pending.values()) waiter.reject(error);
    this.pending.clear();
    this.session = undefined;
    try {
      this.transport.close();
    } catch {
      // Already closed.
    }
    this.onClose?.(code);
  }

  /**
   * Asks Erapkey to allow this extension. `onCode` gets the six digits to show while the user compares them
   * with the Erapkey window. Returns the pairing to keep.
   */
  async associate(name: string, keys: CryptoKeyPair, onCode: (code: string) => void): Promise<Pairing> {
    const extensionPublic = await exportPublicKey(keys.publicKey);
    this.sendPlain({ type: 'associate', version: PROTOCOL_VERSION, name, publicKey: toBase64(extensionPublic) });

    const pairing = await this.nextPlain();
    if (pairing.type !== 'pairing') throw new HostError(pairing.error ?? 'bad_request');
    onCode(await pairingCode(fromBase64(pairing.appPublicKey), extensionPublic));

    // Erapkey waits up to two minutes for the user.
    const associated = await this.nextPlain(150_000);
    if (associated.type !== 'associated' || !associated.ok) throw new HostError(associated.error ?? 'bad_request');
    return { keys, pairingId: associated.pairingId, appPublicKey: pairing.appPublicKey };
  }

  /** Opens the encrypted session of a pairing. Throws HostError('not_paired') when Erapkey forgot it. */
  async hello(pairing: Pairing): Promise<void> {
    if (!pairing.pairingId || !pairing.appPublicKey) throw new HostError('not_paired');
    const ephemeral = await generateKeyPair();
    const extensionNonce = randomBytes(NONCE_SIZE);
    this.sendPlain({
      type: 'hello',
      version: PROTOCOL_VERSION,
      pairingId: pairing.pairingId,
      ephemeralKey: toBase64(await exportPublicKey(ephemeral.publicKey)),
      nonce: toBase64(extensionNonce),
    });

    const answer = await this.nextPlain();
    if (answer.type !== 'hello' || !answer.ok) throw new HostError(answer.error ?? 'bad_request');

    this.deriving = true;
    try {
      this.session = await deriveSessionKeys(
        pairing.keys.privateKey, await importPublicKey(fromBase64(pairing.appPublicKey)),
        ephemeral.privateKey, await importPublicKey(fromBase64(answer.ephemeralKey)),
        extensionNonce, fromBase64(answer.nonce));
    } finally {
      this.deriving = false;
    }
    for (const message of this.early.splice(0)) this.receive(message);
  }

  async status(): Promise<{ locked: boolean; version: number }> {
    return this.request({ type: 'status' });
  }

  /** Waits for the user to unlock Erapkey (up to a minute). */
  async unlock(): Promise<boolean> {
    const answer = await this.request({ type: 'unlock' });
    return !answer.locked;
  }

  async getLogins(url: string): Promise<Login[]> {
    return (await this.request({ type: 'getLogins', url })).logins;
  }

  async getCredentials(entryId: string, url: string): Promise<Credentials> {
    return this.request({ type: 'getCredentials', entryId, url });
  }

  async getTotp(entryId: string, url: string): Promise<{ code: string; validFor: number }> {
    return this.request({ type: 'getTotp', entryId, url });
  }

  async openEntry(entryId: string): Promise<void> {
    await this.request({ type: 'openEntry', entryId });
  }

  private async request(body: Record<string, unknown>): Promise<any> {
    if (!this.session || !this.isOpen) throw new HostError(this.closedWith ?? 'not_paired');
    const id = this.nextId++;
    const answer = new Promise<any>((resolve, reject) => this.pending.set(id, { resolve, reject }));
    await this.sendEncrypted({ ...body, id });
    const message = await answer;
    if (!message.ok) throw new HostError(message.error ?? 'internal');
    return message;
  }

  private sendPlain(message: unknown): void {
    if (!this.isOpen) throw new HostError(this.closedWith ?? 'disconnected');
    this.transport.send(message);
  }

  private nextPlain(timeout = PLAIN_TIMEOUT): Promise<any> {
    const buffered = this.plainMessages.shift();
    if (buffered !== undefined) return Promise.resolve(buffered);
    if (!this.isOpen) return Promise.reject(new HostError(this.closedWith ?? 'disconnected'));
    return new Promise((resolve, reject) => {
      const waiter: Waiter = {
        resolve: (message) => { clearTimeout(timer); resolve(message); },
        reject: (error) => { clearTimeout(timer); reject(error); },
      };
      const timer = setTimeout(() => {
        const index = this.plainWaiters.indexOf(waiter);
        if (index >= 0) this.plainWaiters.splice(index, 1);
        reject(new HostError('timeout'));
      }, timeout);
      this.plainWaiters.push(waiter);
    });
  }

  private sendEncrypted(message: unknown): Promise<void> {
    const session = this.session!;
    this.sendChain = this.sendChain.then(async () => {
      const counter = this.sendCounter++;
      const sealed = await seal(session.sendKey, counter, new TextEncoder().encode(JSON.stringify(message)));
      this.sendPlain({ n: counter, c: toBase64(sealed) });
    });
    return this.sendChain;
  }

  private async receiveEncrypted(envelope: any): Promise<void> {
    const session = this.session;
    if (!session) return;
    if (envelope?.n !== this.receiveCounter || typeof envelope.c !== 'string') {
      this.close('bad_request');
      return;
    }
    const plaintext = await open(session.receiveKey, this.receiveCounter++, fromBase64(envelope.c));
    const message = JSON.parse(new TextDecoder().decode(plaintext));
    if (typeof message.event === 'string') {
      this.onEvent?.(message.event);
      return;
    }
    const waiter = this.pending.get(message.id);
    if (waiter) {
      this.pending.delete(message.id);
      waiter.resolve(message);
    }
  }
}
