// Messages between the toolbar popup and the background service worker.
import type { ErrorCode, Login } from './client';

export type PopupRequest =
  | { cmd: 'state'; tabId: number }
  | { cmd: 'pair' }
  | { cmd: 'unlock' }
  | { cmd: 'fill'; tabId: number; entryId: string }
  | { cmd: 'open'; entryId: string };

/** What the popup shows. */
export type State =
  | { state: 'unlocked'; host?: string; logins: Login[] }
  | { state: 'locked' }
  | { state: 'pairing'; code?: string }
  | { state: 'error'; error: ErrorCode };

export type FillResult = { ok: true; filled: boolean } | { ok: false; error: ErrorCode | 'no_fields' | 'no_totp' | 'page_changed' };
