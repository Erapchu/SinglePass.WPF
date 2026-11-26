// Builds the extension and starts Chrome or Edge with it loaded, in a separate profile under .profile/.
// Rebuilds and reloads the extension on every change under src/. Closing the browser or Ctrl+C stops it.
//   node run.mjs [chrome|edge] [--release] [url...]
//     chrome|edge  the browser (default: Chrome if installed, otherwise Edge)
//     --release    talk to the installed (Release) Erapkey instead of Debug
//     url          pages to open (default: the browser's extensions page)
//
// Branded Chrome ignores --load-extension since version 137, so the extension is loaded through the DevTools
// protocol (Extensions.loadUnpacked over --remote-debugging-pipe), which works in Chrome and Edge alike.
import { execFileSync, spawn } from 'node:child_process';
import { existsSync, mkdirSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { build, distPath, DEV_EXTENSION_ID, HOST_NAMES, watchSources } from './build.mjs';

const BROWSERS = {
  chrome: {
    title: 'Chrome',
    appPath: 'chrome.exe',
    paths: [
      `${process.env.ProgramFiles}\\Google\\Chrome\\Application\\chrome.exe`,
      `${process.env['ProgramFiles(x86)']}\\Google\\Chrome\\Application\\chrome.exe`,
      `${process.env.LOCALAPPDATA}\\Google\\Chrome\\Application\\chrome.exe`,
    ],
    registry: 'HKCU\\Software\\Google\\Chrome\\NativeMessagingHosts',
    extensionsPage: 'chrome://extensions',
  },
  edge: {
    title: 'Edge',
    appPath: 'msedge.exe',
    paths: [
      `${process.env['ProgramFiles(x86)']}\\Microsoft\\Edge\\Application\\msedge.exe`,
      `${process.env.ProgramFiles}\\Microsoft\\Edge\\Application\\msedge.exe`,
    ],
    registry: 'HKCU\\Software\\Microsoft\\Edge\\NativeMessagingHosts',
    extensionsPage: 'edge://extensions',
    // Otherwise Edge restarts itself in a new process, which does not get the DevTools pipe.
    flags: ['--edge-skip-compat-layer-relaunch'],
  },
};

const args = process.argv.slice(2);
const host = args.includes('--release') ? 'release' : 'dev';
const named = args.find(arg => arg in BROWSERS);
const urls = args.filter(arg => !arg.startsWith('--') && !(arg in BROWSERS));

const browserName = named ?? (findExecutable(BROWSERS.chrome) ? 'chrome' : 'edge');
const browser = BROWSERS[browserName];
const executable = findExecutable(browser);
if (!executable) {
  console.error(`${browser.title} is not installed.`);
  process.exit(1);
}

checkHostRegistered();

// Release Erapkey allows the development ID too until the stores assign theirs, so both builds carry the key.
await build({ host, key: true });

const profile = fileURLToPath(new URL(`.profile/${browserName}-${host}`, import.meta.url));
mkdirSync(profile, { recursive: true });

// stdio 3 and 4 are the DevTools pipe: the browser reads commands from 3 and writes replies to 4.
const child = spawn(executable, [
  `--user-data-dir=${profile}`,
  '--no-first-run',
  '--no-default-browser-check',
  '--remote-debugging-pipe',
  '--enable-unsafe-extension-debugging',
  ...(browser.flags ?? []),
  ...(urls.length ? urls : [browser.extensionsPage]),
], { stdio: ['ignore', 'ignore', 'ignore', 'pipe', 'pipe'] });

const devtools = connectDevTools(child.stdio[3], child.stdio[4]);
let loaded = false;
let stopWatching = () => {};

child.once('exit', () => {
  stopWatching();
  if (!loaded) {
    // A second browser process with the same profile hands over to the first one and exits.
    console.error(`${browser.title} exited at once. Is it already open with ${profile}? Close that window and run again.`);
    process.exit(1);
  }
  console.log(`${browser.title} closed.`);
  process.exit(0);
});
process.once('SIGINT', () => child.kill());

try {
  const id = await loadExtension();
  loaded = true;
  console.log(`${browser.title} started with the extension ${id} (profile ${profile}).`);
  if (id !== DEV_EXTENSION_ID)
    console.warn(`Warning: Erapkey allows only ${DEV_EXTENSION_ID}; this ID will be refused.`);
} catch (error) {
  if (child.exitCode !== null)
    await new Promise(() => {}); // The exit handler reports it.
  console.error(`Could not load the extension: ${error.message}`);
  child.kill();
  process.exit(1);
}

stopWatching = watchSources({ host, key: true }, async () => {
  await loadExtension();
  console.log(`${new Date().toLocaleTimeString()} rebuilt and reloaded; reopen the popup to see the change.`);
});
console.log('Watching src/ for changes; close the browser or press Ctrl+C to stop.');

/** Loading the same folder again replaces the loaded copy, which is how a rebuild is reloaded. */
async function loadExtension() {
  const { id } = await devtools('Extensions.loadUnpacked', { path: distPath });
  return id;
}

function findExecutable({ appPath, paths }) {
  try {
    // The App Paths key covers installs in unusual folders.
    const output = execFileSync('reg', ['query', `HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\App Paths\\${appPath}`, '/ve'],
      { encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] });
    const match = /REG_SZ\s+(.+)/.exec(output);
    const path = match?.[1].trim().replace(/^"|"$/g, '');
    if (path && existsSync(path))
      return path;
  } catch {
    // Not registered there.
  }
  return paths.find(path => path && existsSync(path));
}

/** Erapkey registers the native messaging host when the browser extension setting is on. */
function checkHostRegistered() {
  try {
    execFileSync('reg', ['query', `${browser.registry}\\${HOST_NAMES[host]}`], { stdio: 'ignore' });
  } catch {
    const app = host === 'dev' ? 'Debug Erapkey' : 'Erapkey';
    console.warn(`Note: ${HOST_NAMES[host]} is not registered for ${browser.title}. ` +
      `Start ${app} and turn on Settings → Security → Browser extension, or the extension will say Erapkey is not running.`);
  }
}

/** A minimal DevTools protocol client over the pipe: JSON messages separated by NUL. */
function connectDevTools(input, output) {
  let nextId = 1;
  let buffer = '';
  const pending = new Map();

  output.setEncoding('utf8');
  output.on('data', chunk => {
    buffer += chunk;
    let end;
    while ((end = buffer.indexOf('\0')) >= 0) {
      const message = JSON.parse(buffer.slice(0, end));
      buffer = buffer.slice(end + 1);
      const request = pending.get(message.id);
      if (!request)
        continue;
      pending.delete(message.id);
      if (message.error)
        request.reject(new Error(message.error.message));
      else
        request.resolve(message.result);
    }
  });
  output.on('close', () => {
    for (const request of pending.values())
      request.reject(new Error('the browser closed the DevTools pipe'));
    pending.clear();
  });
  input.on('error', () => {});

  return (method, params) => new Promise((resolve, reject) => {
    const id = nextId++;
    pending.set(id, { resolve, reject });
    input.write(JSON.stringify({ id, method, params }) + '\0');
  });
}
