// The pairing lives in IndexedDB: chrome.storage holds JSON only, and the private key must stay a
// non-extractable CryptoKey (docs/browser-extension.md#pairing). Nothing from the vault is stored.
import { generateKeyPair } from './crypto';
import type { Pairing } from './client';

const DATABASE = 'erapkey';
const STORE = 'pairing';
const KEY = 'current';

function openDatabase(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    const request = indexedDB.open(DATABASE, 1);
    request.onupgradeneeded = () => request.result.createObjectStore(STORE);
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });
}

async function transaction<T>(mode: IDBTransactionMode, run: (store: IDBObjectStore) => IDBRequest<T>): Promise<T> {
  const database = await openDatabase();
  try {
    return await new Promise<T>((resolve, reject) => {
      const request = run(database.transaction(STORE, mode).objectStore(STORE));
      request.onsuccess = () => resolve(request.result);
      request.onerror = () => reject(request.error);
    });
  } finally {
    database.close();
  }
}

/** The pairing of this browser profile; the key pair is made on first use. */
export async function loadPairing(): Promise<Pairing> {
  const stored = await transaction<Pairing | undefined>('readonly', (store) => store.get(KEY));
  if (stored?.keys) return stored;
  const pairing: Pairing = { keys: await generateKeyPair() };
  await savePairing(pairing);
  return pairing;
}

export async function savePairing(pairing: Pairing): Promise<void> {
  await transaction('readwrite', (store) => store.put(pairing, KEY));
}

/** Erapkey no longer knows the pairing: keep the key pair, forget the rest. */
export async function forgetPairing(pairing: Pairing): Promise<void> {
  await savePairing({ keys: pairing.keys });
}
