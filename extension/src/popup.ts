// The toolbar popup: status, pairing, unlock, and the logins of the current tab. Everything goes through the
// background worker; the popup keeps nothing after it closes.
import type { ErrorCode, Login } from './client';
import type { FillResult, PopupRequest, State } from './messages';

const content = document.getElementById('content')!;
const hostLabel = document.getElementById('host')!;
const rowTemplate = document.getElementById('login-row') as HTMLTemplateElement;
let tabId = -1;
let pollTimer: number | undefined;

const t = (key: string, ...substitutions: string[]): string => chrome.i18n.getMessage(key, substitutions) || key;

function send<T>(request: PopupRequest): Promise<T> {
  return chrome.runtime.sendMessage(request) as Promise<T>;
}

function element<K extends keyof HTMLElementTagNameMap>(tag: K, text?: string, className?: string): HTMLElementTagNameMap[K] {
  const node = document.createElement(tag);
  if (text !== undefined) node.textContent = text;
  if (className) node.className = className;
  return node;
}

function show(...nodes: Node[]): void {
  content.replaceChildren(...nodes);
}

function button(text: string, onClick: () => void): HTMLButtonElement {
  const node = element('button', text, 'primary');
  node.type = 'button';
  node.addEventListener('click', onClick);
  return node;
}

function errorText(error: ErrorCode | string): string {
  switch (error) {
    case 'app_not_running': return t('errorNotRunning');
    case 'denied': return t('errorDenied');
    case 'timeout': return t('errorTimeout');
    case 'unsupported_version': return t('errorVersion');
    case 'not_found': return t('errorNotFound');
    case 'no_fields': return t('errorNoFields');
    case 'page_changed': return t('errorPageChanged');
    case 'no_totp': return t('errorNoTotp');
    case 'locked': return t('locked');
    default: return t('errorGeneric', error);
  }
}

async function refresh(): Promise<void> {
  render(await send<State>({ cmd: 'state', tabId }));
}

function render(state: State): void {
  window.clearTimeout(pollTimer);
  hostLabel.textContent = state.state === 'unlocked' ? state.host ?? '' : '';

  switch (state.state) {
    case 'error':
      if (state.error === 'not_paired') {
        show(element('p', t('notPaired')), button(t('pair'), startPairing));
      } else {
        show(element('p', errorText(state.error), 'error'), button(t('retry'), () => void refresh()));
      }
      return;

    case 'pairing': {
      const nodes: Node[] = [element('p', t('pairingHint'))];
      if (state.code) nodes.push(element('div', `${state.code.slice(0, 3)} ${state.code.slice(3)}`, 'code'));
      nodes.push(element('p', t('pairingWaiting'), 'muted'));
      show(...nodes);
      // The pairing goes on in the background worker; the popup only shows it.
      pollTimer = window.setTimeout(() => void refresh(), 700);
      return;
    }

    case 'locked': {
      const unlock = button(t('unlock'), async () => {
        unlock.disabled = true;
        unlock.textContent = t('unlockWaiting');
        await send({ cmd: 'unlock' });
        await refresh();
      });
      show(element('p', t('locked')), unlock);
      return;
    }

    case 'unlocked':
      if (!state.host) show(element('p', t('noSite'), 'muted'));
      else if (state.logins.length === 0) show(element('p', t('noLogins', state.host), 'muted'));
      else show(loginList(state.logins));
      return;
  }
}

function startPairing(): void {
  void send({ cmd: 'pair' }).then(() => refresh());
}

function loginList(logins: Login[]): HTMLElement {
  const list = element('ul');
  for (const login of logins) {
    const row = rowTemplate.content.firstElementChild!.cloneNode(true) as HTMLLIElement;
    row.querySelector('.name')!.textContent = login.name || login.login;
    row.querySelector('.user')!.textContent = login.login;

    const fillButton = row.querySelector<HTMLButtonElement>('.fill')!;
    fillButton.title = t('fill');
    fillButton.addEventListener('click', async () => {
      const result = await send<FillResult>({ cmd: 'fill', tabId, entryId: login.entryId });
      if (result.ok) window.close();
      else toast(errorText(result.error));
    });

    list.append(row);
  }
  return list;
}

function toast(text: string): void {
  content.querySelector('.toast')?.remove();
  content.append(element('p', text, 'toast'));
}

async function main(): Promise<void> {
  for (const node of document.querySelectorAll<HTMLElement>('[data-i18n]')) node.textContent = t(node.dataset.i18n!);
  const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
  tabId = tab?.id ?? -1;
  await refresh();
}

void main();
