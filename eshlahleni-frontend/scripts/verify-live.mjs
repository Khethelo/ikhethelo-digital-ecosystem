import { spawn } from 'node:child_process';
import { generateKeyPairSync, randomBytes, randomUUID } from 'node:crypto';
import { createServer } from 'node:net';
import { fileURLToPath } from 'node:url';
import { preview } from './preview.mjs';
const root = fileURLToPath(new URL('../', import.meta.url));
const workspace = fileURLToPath(new URL('../../', import.meta.url));
if (!process.env.IKD_TEST_POSTGRES) throw new Error('Set IKD_TEST_POSTGRES to an isolated PostgreSQL control connection. Never use your club database.');
const database = 'eshl_frontend_e2e_' + randomUUID().replaceAll('-', '');
const env = { ...process.env, ESHL_TEST_DATABASE: database, ESHL_E2E_EMAIL: 'frontend-admin@example.test', ESHL_E2E_PASSWORD: randomBytes(28).toString('base64url') + '!aA1' };
const helper = fileURLToPath(new URL('./verification/bin/Release/net10.0/FrontendVerification.dll', import.meta.url));
async function command(exe, args, options = {}) {
  const child = spawn(exe, args, { cwd: root, env, stdio: 'pipe', windowsHide: true, ...options });
  // Setup and API output are intentionally not forwarded: they may contain submitted contact data.
  let output = ''; child.stdout?.on('data', chunk => { output += chunk; }); child.stderr?.on('data', chunk => { output += chunk; });
  await new Promise((done, reject) => { child.once('error', reject); child.once('exit', code => code === 0 ? done() : reject(new Error(`Verification command failed (${exe}, exit ${code}). Credentials and server output withheld.`))); });
}
async function port() { const listener = createServer(); await new Promise(done => listener.listen(0, '127.0.0.1', done)); const value = listener.address().port; await new Promise(done => listener.close(done)); return value; }
let api, server;
try {
  await command('dotnet', ['build', 'scripts/verification/FrontendVerification.csproj', '-c', 'Release', '--nologo']);
  await command('dotnet', [helper]); console.log('Disposable schema and temporary administrator ready.');
  const apiPort = await port(); const webPort = await port();
  const { privateKey } = generateKeyPairSync('rsa', { modulusLength: 2048, privateKeyEncoding: { type: 'pkcs8', format: 'pem' }, publicKeyEncoding: { type: 'spki', format: 'pem' } });
  const apiEnv = { ...env, ASPNETCORE_ENVIRONMENT: 'Development', ConnectionStrings__DefaultConnection: env.IKD_TEST_POSTGRES.replace(/Database\s*=\s*[^;]+/i, `Database=${database}`), Jwt__Issuer: 'http://frontend-verification', Jwt__Audience: 'frontend-verification', Jwt__PrivateKeyPem: privateKey, RateLimits__PerMinute: '1000' };
  if (!apiEnv.ConnectionStrings__DefaultConnection.includes(`Database=${database}`)) throw new Error('Control connection must explicitly name its database.');
  api = spawn('dotnet', [workspace + 'IKhethelo.Digital.Api/src/IKhethelo.Digital.Api/bin/Release/net10.0/IKhethelo.Digital.Api.dll', '--urls', `http://127.0.0.1:${apiPort}`], { cwd: workspace + 'IKhethelo.Digital.Api/src/IKhethelo.Digital.Api', env: apiEnv, stdio: 'ignore', windowsHide: true });
  api.on('error', () => {});
  let ready = false;
  for (let attempt = 0; attempt < 100; attempt++) { try { await fetch(`http://127.0.0.1:${apiPort}/health`); ready = true; break; } catch { await new Promise(done => setTimeout(done, 300)); } }
  if (!ready) throw new Error('Isolated API did not start. No production configuration was changed.');
  const cors = await fetch(`http://127.0.0.1:${apiPort}/api/v1/auth/login`, { method: 'OPTIONS', headers: { Origin: 'http://localhost:4200', 'Access-Control-Request-Method': 'POST', 'Access-Control-Request-Headers': 'content-type' } });
  if (cors.headers.get('access-control-allow-origin') !== 'http://localhost:4200') throw new Error('Existing Development CORS does not permit the frontend origin. Report the contract/configuration issue before editing the backend.');
  console.log('Existing Development CORS permits http://localhost:4200.');
  server = await preview(`http://127.0.0.1:${apiPort}`, webPort);
  env.ESHL_BASE_URL = `http://127.0.0.1:${webPort}`;
  await command(process.execPath, ['node_modules/@playwright/test/cli.js', 'test', ...process.argv.slice(2)], { stdio: 'inherit' });
  console.log('Real-browser registration, review and membership acceptance tests passed.');
} finally {
  if (server) await new Promise(done => server.close(done));
  if (api && api.exitCode === null) { api.kill(); await new Promise(done => { api.once('exit', done); setTimeout(done, 3000); }); }
  await command('dotnet', [helper, 'cleanup']); console.log('Disposable frontend database removed.');
}
