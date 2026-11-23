// Builds the extension into dist/ (load it unpacked from there).
//   node build.mjs        release: talks to com.singlepass.host
//   node build.mjs --dev  development: talks to com.singlepass.host.dev (Debug SinglePass) and has a fixed key,
//                         so the unpacked extension always gets the ID that SinglePass allows
//   node build.mjs --test the pipe test client for Node (dist-test/)
import * as esbuild from 'esbuild';
import { cp, mkdir, readFile, rm, writeFile } from 'node:fs/promises';

const dev = process.argv.includes('--dev');
const test = process.argv.includes('--test');

// The public half of the development key; the extension ID derived from it is in NativeMessagingRegistration.AllowedOrigins.
const DEV_KEY =
  'MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAwsuTK2CVXmODK4NAhO2nihaYMw6AWa6qRkN2GMuOcNvSWgjcpEi4EV3OLyNdB8GaP/v2eNCck3RsrcVr/9+m7gZjqkTlORH1vOY9flYDfQgzgCo7xgwOPN2Tx7dZtwB4CwcZ7oazM/3smXxWXxKixrjdU5nPoW0BRt8f9+3oOjVyonvsPbtEfSHdOGPii4o6gjzYzr0hzxoUCzcN++U/MRIXlFxbgMQlTWPmYHbIsat2/6LQ9EJZ1UeM9GkHrIPGDdevbZQpFEDgwul/I1wMm+MUI3c93RDo3hiopIFzZkuhMoNaAsRdJYakKZgw9LQdrjDf8DekyyxC3d1Hsm09NQIDAQAB';

if (test) {
  await rm('dist-test', { recursive: true, force: true });
  await esbuild.build({
    entryPoints: ['test/pipe-client.ts'],
    outdir: 'dist-test',
    bundle: true,
    platform: 'node',
    format: 'esm',
    target: 'node20',
  });
  process.exit(0);
}

await rm('dist', { recursive: true, force: true });
await mkdir('dist', { recursive: true });

await esbuild.build({
  entryPoints: { background: 'src/background.ts', popup: 'src/popup.ts' },
  outdir: 'dist',
  bundle: true,
  format: 'esm',
  target: 'chrome120',
  sourcemap: dev ? 'inline' : false,
  minify: !dev,
  define: { __HOST_NAME__: JSON.stringify(dev ? 'com.singlepass.host.dev' : 'com.singlepass.host') },
});

const manifest = JSON.parse(await readFile('src/manifest.json', 'utf8'));
const pkg = JSON.parse(await readFile('package.json', 'utf8'));
manifest.version = pkg.version;
if (dev) {
  manifest.key = DEV_KEY;
  manifest.name = '__MSG_extensionNameDev__';
}
await writeFile('dist/manifest.json', JSON.stringify(manifest, null, 2));

await cp('src/popup.html', 'dist/popup.html');
await cp('src/popup.css', 'dist/popup.css');
await cp('src/_locales', 'dist/_locales', { recursive: true });
await cp('src/icons', 'dist/icons', { recursive: true });

console.log(`Built ${dev ? 'development' : 'release'} extension into dist/`);
