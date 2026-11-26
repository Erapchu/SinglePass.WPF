// Runs the extension's protocol code (client.ts, crypto.ts with WebCrypto) against a real BrowserPipeServer.
// Started by Erapkey.Core.Tests/BrowserExtensionInteropTests with the pipe name and the expected answers:
//   node dist-test/pipe-client.js <pipe name> <entry id> <login> <password>
// Prints "OK" and exits with 0 when every step answers as expected.
import { connect } from 'node:net';
import { Client } from '../src/client';
import { generateKeyPair } from '../src/crypto';

const [pipeName, entryId, expectedLogin, expectedPassword] = process.argv.slice(2);

function fail(message: string): never {
  console.error(`FAIL: ${message}`);
  process.exit(1);
}

const socket = connect(`\\\\.\\pipe\\${pipeName}`);
const client = new Client({
  send: (message) => {
    const payload = Buffer.from(JSON.stringify(message), 'utf8');
    const header = Buffer.alloc(4);
    header.writeUInt32BE(payload.length);
    socket.write(Buffer.concat([header, payload]));
  },
  close: () => socket.end(),
});

// The pipe is a byte stream: split it into frames of a 32-bit big-endian length and JSON.
let buffer = Buffer.alloc(0);
socket.on('data', (chunk) => {
  buffer = Buffer.concat([buffer, chunk]);
  while (buffer.length >= 4 && buffer.length >= 4 + buffer.readUInt32BE(0)) {
    const length = buffer.readUInt32BE(0);
    client.receive(JSON.parse(buffer.subarray(4, 4 + length).toString('utf8')));
    buffer = buffer.subarray(4 + length);
  }
});
socket.on('close', () => client.close());

await new Promise<void>((resolve, reject) => {
  socket.once('connect', resolve);
  socket.once('error', reject);
});

let code = '';
const pairing = await client.associate('Node test', await generateKeyPair(), (value) => (code = value));
if (!/^\d{6}$/.test(code)) fail(`pairing code ${code}`);
console.log(`CODE ${code}`);

await client.hello(pairing);

const status = await client.status();
if (status.locked) fail('locked');

const logins = await client.getLogins('https://login.example.com/');
if (logins.length !== 1 || logins[0].entryId !== entryId || logins[0].match !== 'domain') fail(`logins ${JSON.stringify(logins)}`);

const credentials = await client.getCredentials(entryId, 'https://example.com/');
if (credentials.login !== expectedLogin || credentials.password !== expectedPassword) fail('credentials');

try {
  await client.getCredentials(entryId, 'https://example.com.evil.net/');
  fail('a look-alike page got credentials');
} catch (error) {
  if ((error as Error).message !== 'not_found') fail(`look-alike: ${error}`);
}

console.log('OK');
client.close();
process.exit(0);
