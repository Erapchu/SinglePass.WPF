// Builds the extension into dist/ (load it unpacked from there).
//   node build.mjs          release: talks to com.singlepass.host, no key (the store assigns the ID)
//   node build.mjs --dev    development: talks to com.singlepass.host.dev (Debug SinglePass) and has a fixed key,
//                           so the unpacked extension always gets the ID that SinglePass allows
//   node build.mjs --watch  with --dev: rebuild on every change under src/
//   node build.mjs --test   the pipe test client for Node (dist-test/)
// run.mjs builds through build() below and starts a browser with the extension.
import * as esbuild from 'esbuild';
import { watch } from 'node:fs';
import { cp, mkdir, readFile, rm, writeFile } from 'node:fs/promises';
import { fileURLToPath, pathToFileURL } from 'node:url';

const root = fileURLToPath(new URL('.', import.meta.url));
const src = root + 'src';
export const distPath = root + 'dist';

// The public half of the development key; the extension ID derived from it is in NativeMessagingRegistration.AllowedOrigins.
const DEV_KEY =
  'MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAwsuTK2CVXmODK4NAhO2nihaYMw6AWa6qRkN2GMuOcNvSWgjcpEi4EV3OLyNdB8GaP/v2eNCck3RsrcVr/9+m7gZjqkTlORH1vOY9flYDfQgzgCo7xgwOPN2Tx7dZtwB4CwcZ7oazM/3smXxWXxKixrjdU5nPoW0BRt8f9+3oOjVyonvsPbtEfSHdOGPii4o6gjzYzr0hzxoUCzcN++U/MRIXlFxbgMQlTWPmYHbIsat2/6LQ9EJZ1UeM9GkHrIPGDdevbZQpFEDgwul/I1wMm+MUI3c93RDo3hiopIFzZkuhMoNaAsRdJYakKZgw9LQdrjDf8DekyyxC3d1Hsm09NQIDAQAB';

/** The ID that DEV_KEY gives; SinglePass allows only it until the stores assign theirs. */
export const DEV_EXTENSION_ID = 'mhnlllocpadmflggkijecmklkooflbbe';

export const HOST_NAMES = { dev: 'com.singlepass.host.dev', release: 'com.singlepass.host' };

/**
 * Builds dist/.
 * host: 'dev' for Debug SinglePass, 'release' for the installed one.
 * key: put DEV_KEY into the manifest (every unpacked build; never a store build).
 * clean: delete dist/ first (not while a browser has it loaded).
 */
export async function build({ host = 'dev', key = host === 'dev', clean = true } = {}) {
  const dev = host === 'dev';
  if (clean)
    await rm(distPath, { recursive: true, force: true });
  await mkdir(distPath, { recursive: true });

  await esbuild.build({
    entryPoints: { background: `${src}/background.ts`, popup: `${src}/popup.ts` },
    outdir: distPath,
    bundle: true,
    format: 'esm',
    target: 'chrome120',
    sourcemap: key ? 'inline' : false,
    minify: !key,
    logLevel: 'error',
    define: { __HOST_NAME__: JSON.stringify(HOST_NAMES[host]) },
  });

  const manifest = JSON.parse(await readFile(`${src}/manifest.json`, 'utf8'));
  const pkg = JSON.parse(await readFile(`${root}package.json`, 'utf8'));
  manifest.version = pkg.version;
  if (key)
    manifest.key = DEV_KEY;
  if (dev)
    manifest.name = '__MSG_extensionNameDev__';
  await writeFile(`${distPath}/manifest.json`, JSON.stringify(manifest, null, 2));

  await cp(`${src}/popup.html`, `${distPath}/popup.html`);
  await cp(`${src}/popup.css`, `${distPath}/popup.css`);
  await cp(`${src}/_locales`, `${distPath}/_locales`, { recursive: true });
  await cp(`${src}/icons`, `${distPath}/icons`, { recursive: true });
}

/** Calls onChange (after a build) whenever something under src/ changes; returns a function that stops watching. */
export function watchSources(options, onChange) {
  let timer;
  let running = Promise.resolve();
  const watcher = watch(src, { recursive: true }, () => {
    clearTimeout(timer);
    timer = setTimeout(() => {
      running = running.then(async () => {
        try {
          await build({ ...options, clean: false });
          await onChange?.();
        } catch (error) {
          // esbuild has printed the error; keep watching for the fix.
          console.error(`Build failed: ${error.message.split('\n')[0]}`);
        }
      });
    }, 150);
  });
  return () => {
    clearTimeout(timer);
    watcher.close();
  };
}

async function buildTestClient() {
  await rm(`${root}dist-test`, { recursive: true, force: true });
  await esbuild.build({
    entryPoints: [`${root}test/pipe-client.ts`],
    outdir: `${root}dist-test`,
    bundle: true,
    platform: 'node',
    format: 'esm',
    target: 'node20',
  });
}

if (import.meta.url === pathToFileURL(process.argv[1]).href) {
  const args = process.argv.slice(2);
  if (args.includes('--test')) {
    await buildTestClient();
  } else {
    const dev = args.includes('--dev');
    const options = { host: dev ? 'dev' : 'release' };
    await build(options);
    console.log(`Built ${dev ? 'development' : 'release'} extension into dist/`);
    if (args.includes('--watch')) {
      watchSources(options, () => console.log(`${new Date().toLocaleTimeString()} rebuilt`));
      console.log('Watching src/ for changes; press Ctrl+C to stop.');
    }
  }
}
