// Key agreement and message encryption with Erapkey (docs/browser-extension.md#session).
// Mirrors Erapkey.Core/Services/Browser/BrowserCrypto.cs: ECDH P-256 with SPKI public keys,
// HKDF-SHA256 and AES-256-GCM with a 12-byte nonce of 4 zero bytes and a 64-bit big-endian counter.

const subtle = globalThis.crypto.subtle;
const encoder = new TextEncoder();
const PAIRING_LABEL = encoder.encode('erapkey-pair');
const SESSION_INFO = encoder.encode('erapkey-browser-v1');
const ECDH_P256 = { name: 'ECDH', namedCurve: 'P-256' } as const;

export const NONCE_SIZE = 16;

/** A key pair whose private half never leaves WebCrypto. */
export function generateKeyPair(): Promise<CryptoKeyPair> {
  return subtle.generateKey(ECDH_P256, false, ['deriveBits']) as Promise<CryptoKeyPair>;
}

export async function exportPublicKey(key: CryptoKey): Promise<Uint8Array> {
  return new Uint8Array(await subtle.exportKey('spki', key));
}

export function importPublicKey(spki: Uint8Array): Promise<CryptoKey> {
  return subtle.importKey('spki', spki, ECDH_P256, true, []);
}

export function randomBytes(length: number): Uint8Array {
  return globalThis.crypto.getRandomValues(new Uint8Array(length));
}

/** The six digits Erapkey shows too: SHA-256("erapkey-pair" || appPublic || extensionPublic), first 4 bytes mod 1 000 000. */
export async function pairingCode(appPublicKey: Uint8Array, extensionPublicKey: Uint8Array): Promise<string> {
  const hash = new DataView(await subtle.digest('SHA-256', concat(PAIRING_LABEL, appPublicKey, extensionPublicKey)));
  return String(hash.getUint32(0) % 1_000_000).padStart(6, '0');
}

export interface SessionKeys {
  /** Extension → Erapkey. */
  sendKey: CryptoKey;
  /** Erapkey → extension. */
  receiveKey: CryptoKey;
}

/** secret = ECDH(static) || ECDH(ephemeral); HKDF-SHA256(salt = nonceExt || nonceApp) gives 32 bytes per direction. */
export async function deriveSessionKeys(
  extensionStatic: CryptoKey, appStatic: CryptoKey,
  extensionEphemeral: CryptoKey, appEphemeral: CryptoKey,
  extensionNonce: Uint8Array, appNonce: Uint8Array,
): Promise<SessionKeys> {
  const staticSecret = new Uint8Array(await subtle.deriveBits({ name: 'ECDH', public: appStatic }, extensionStatic, 256));
  const ephemeralSecret = new Uint8Array(await subtle.deriveBits({ name: 'ECDH', public: appEphemeral }, extensionEphemeral, 256));
  const secret = concat(staticSecret, ephemeralSecret);
  const hkdfKey = await subtle.importKey('raw', secret, 'HKDF', false, ['deriveBits']);
  const keys = new Uint8Array(await subtle.deriveBits(
    { name: 'HKDF', hash: 'SHA-256', salt: concat(extensionNonce, appNonce), info: SESSION_INFO }, hkdfKey, 512));
  staticSecret.fill(0);
  ephemeralSecret.fill(0);
  secret.fill(0);
  const [sendKey, receiveKey] = await Promise.all([
    subtle.importKey('raw', keys.subarray(0, 32), 'AES-GCM', false, ['encrypt']),
    subtle.importKey('raw', keys.subarray(32), 'AES-GCM', false, ['decrypt']),
  ]);
  keys.fill(0);
  return { sendKey, receiveKey };
}

export async function seal(key: CryptoKey, counter: number, plaintext: Uint8Array): Promise<Uint8Array> {
  return new Uint8Array(await subtle.encrypt({ name: 'AES-GCM', iv: nonce(counter), tagLength: 128 }, key, plaintext));
}

/** Throws when the message was altered or does not belong to this counter. */
export async function open(key: CryptoKey, counter: number, sealed: Uint8Array): Promise<Uint8Array> {
  return new Uint8Array(await subtle.decrypt({ name: 'AES-GCM', iv: nonce(counter), tagLength: 128 }, key, sealed));
}

function nonce(counter: number): Uint8Array {
  const bytes = new Uint8Array(12);
  new DataView(bytes.buffer).setBigUint64(4, BigInt(counter));
  return bytes;
}

export function concat(...parts: Uint8Array[]): Uint8Array {
  const result = new Uint8Array(parts.reduce((length, part) => length + part.length, 0));
  let offset = 0;
  for (const part of parts) {
    result.set(part, offset);
    offset += part.length;
  }
  return result;
}

export function toBase64(bytes: Uint8Array): string {
  let binary = '';
  for (const byte of bytes) binary += String.fromCharCode(byte);
  return btoa(binary);
}

export function fromBase64(text: string): Uint8Array {
  const binary = atob(text);
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
  return bytes;
}
