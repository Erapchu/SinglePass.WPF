// The background service worker: the native messaging port to Erapkey, the pairing and the session.
// The popup asks it for everything; passwords pass through only for a fill or a copy and are not stored.
import { Client, HostError, type ErrorCode, type Pairing } from './client';
import { fillPage } from './fill';
import type { CopyResult, FillResult, PopupRequest, State } from './messages';
import { forgetPairing, loadPairing, savePairing } from './pairingStore';

declare const __HOST_NAME__: string;

let client: Client | undefined;
let pairingProgress: { code?: string; result?: ErrorCode } | undefined;

/** A connection to Erapkey through a new native host process, unless one is open. */
function connect(): Client {
  if (client?.isOpen) return client;
  const port = chrome.runtime.connectNative(__HOST_NAME__);
  const connection = new Client({
    send: (message) => port.postMessage(message),
    close: () => port.disconnect(),
  });
  port.onMessage.addListener((message) => connection.receive(message));
  port.onDisconnect.addListener(() => {
    // chrome.runtime.lastError tells why: the host is not registered (Erapkey never turned the integration on)
    // or it exited. Either way, Erapkey cannot answer now.
    const reason = chrome.runtime.lastError?.message;
    connection.close(reason && /not found|forbidden/i.test(reason) ? 'app_not_running' : 'disconnected');
  });
  connection.onClose = () => {
    if (client === connection) client = undefined;
  };
  client = connection;
  return connection;
}

/** A connection with an open session; throws HostError('not_paired') when this browser is not paired. */
async function session(): Promise<Client> {
  const connection = connect();
  if (connection.hasSession) return connection;

  const pairing = await loadPairing();
  try {
    await connection.hello(pairing);
  } catch (error) {
    if (error instanceof HostError && error.code === 'not_paired') {
      // Removed in Erapkey, or never paired: pair again.
      if (pairing.pairingId) await forgetPairing(pairing);
    } else {
      connection.close();
    }
    throw error;
  }
  return connection;
}

async function getState(tabId: number): Promise<State> {
  if (pairingProgress) {
    if (!pairingProgress.result) return { state: 'pairing', code: pairingProgress.code };
    // A pairing that failed (denied, timed out) is shown once.
    const error = pairingProgress.result;
    pairingProgress = undefined;
    return { state: 'error', error };
  }
  try {
    const connection = await session();
    const { locked } = await connection.status();
    if (locked) return { state: 'locked' };

    const page = await pageOf(tabId);
    if (!page) return { state: 'unlocked', logins: [] };
    return { state: 'unlocked', host: page.host, logins: await connection.getLogins(page.href) };
  } catch (error) {
    return { state: 'error', error: errorCode(error) };
  }
}

async function pair(): Promise<void> {
  if (pairingProgress && !pairingProgress.result) return;
  pairingProgress = {};
  const progress = pairingProgress;
  try {
    const pairing = await loadPairing();
    // A fresh connection: a session of an old pairing must not be reused.
    client?.close();
    const connection = connect();
    const paired: Pairing = await connection.associate(browserName(), pairing.keys, (code) => (progress.code = code));
    await savePairing(paired);
    await connection.hello(paired);
    pairingProgress = undefined;
  } catch (error) {
    progress.result = errorCode(error);
  }
}

async function fill(tabId: number, entryId: string): Promise<FillResult> {
  try {
    // The address is taken from the tab now, not from what the popup saw.
    const page = await pageOf(tabId);
    if (!page) return { ok: false, error: 'not_found' };
    const credentials = await (await session()).getCredentials(entryId, page.href);
    const [result] = await chrome.scripting.executeScript({
      target: { tabId },
      func: fillPage,
      args: [page.origin, credentials.login, credentials.password],
    });
    const outcome = result?.result;
    if (outcome === 'filled') return { ok: true, filled: true };
    return { ok: false, error: outcome === 'page_changed' ? 'page_changed' : 'no_fields' };
  } catch (error) {
    return { ok: false, error: errorCode(error) };
  }
}

async function copy(tabId: number, entryId: string, field: 'login' | 'password' | 'totp'): Promise<CopyResult> {
  try {
    const page = await pageOf(tabId);
    if (!page) return { ok: false, error: 'not_found' };
    const connection = await session();
    if (field === 'totp') return { ok: true, text: (await connection.getTotp(entryId, page.href)).code };
    const credentials = await connection.getCredentials(entryId, page.href);
    return { ok: true, text: field === 'login' ? credentials.login : credentials.password };
  } catch (error) {
    return { ok: false, error: errorCode(error) };
  }
}

/** The web page of the tab, or undefined for chrome://, file://, the new tab page and others. */
async function pageOf(tabId: number): Promise<URL | undefined> {
  const tab = await chrome.tabs.get(tabId);
  if (!tab.url) return undefined;
  const url = new URL(tab.url);
  return url.protocol === 'https:' || url.protocol === 'http:' ? url : undefined;
}

function browserName(): string {
  const brands = (navigator as Navigator & { userAgentData?: { brands: { brand: string }[] } }).userAgentData?.brands ?? [];
  if (brands.some((b) => b.brand === 'Microsoft Edge')) return 'Edge';
  if (brands.some((b) => b.brand === 'Brave')) return 'Brave';
  return 'Chrome';
}

function errorCode(error: unknown): ErrorCode {
  if (error instanceof HostError) return error.code;
  console.error(error);
  return 'internal';
}

chrome.runtime.onMessage.addListener((request: PopupRequest, sender, sendResponse) => {
  // Only the extension's own pages talk to the background; content scripts and web pages cannot.
  if (sender.id !== chrome.runtime.id || sender.tab) return false;

  const answer = (async (): Promise<unknown> => {
    switch (request.cmd) {
      case 'state':
        return getState(request.tabId);
      case 'pair':
        void pair();
        return { state: 'pairing' } satisfies State;
      case 'unlock':
        try {
          return { ok: await (await session()).unlock() };
        } catch (error) {
          return { ok: false, error: errorCode(error) };
        }
      case 'fill':
        return fill(request.tabId, request.entryId);
      case 'copy':
        return copy(request.tabId, request.entryId, request.field);
      case 'open':
        try {
          await (await session()).openEntry(request.entryId);
          return { ok: true };
        } catch (error) {
          return { ok: false, error: errorCode(error) };
        }
    }
  })();
  answer.then(sendResponse);
  return true;
});
