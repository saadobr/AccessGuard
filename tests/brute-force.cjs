const assert = require('node:assert/strict');
const { spawn } = require('node:child_process');
const fs = require('node:fs');
const path = require('node:path');
const { setTimeout: delay } = require('node:timers/promises');

const project = path.resolve(__dirname, '../AccessGuard');
const dataRoot = path.join(__dirname, '.data');
fs.mkdirSync(dataRoot, { recursive: true });
const data = fs.mkdtempSync(path.join(dataRoot, 'brute-force-'));
const port = process.env.ACCESSGUARD_TEST_PORT || '5417';
const ipv4 = `http://127.0.0.1:${port}`;
const ipv6 = `http://[::1]:${port}`;
const account = { email: 'admin@accessguard.test', password: 'Test-Only!Identity2026' };
let server;
let logs = '';

function client(base) {
  const cookies = new Map();
  let token;
  return async (route, body) => {
    const headers = new Headers({ Cookie: [...cookies].map(([name, value]) => `${name}=${value}`).join('; ') });
    if (body) {
      headers.set('Content-Type', 'application/json');
      headers.set('X-CSRF-TOKEN', token);
    }
    const response = await fetch(base + route, { method: body ? 'POST' : 'GET', headers, body: body && JSON.stringify(body) });
    for (const cookie of response.headers.getSetCookie()) {
      const pair = cookie.split(';', 1)[0];
      const separator = pair.indexOf('=');
      cookies.set(pair.slice(0, separator), pair.slice(separator + 1));
    }
    if (route === '/api/auth/session') token = (await response.clone().json()).token;
    return response;
  };
}

async function start() {
  server = spawn(process.env.DOTNET_EXE || 'dotnet', [
    path.join(project, 'bin/Debug/net10.0/AccessGuard.dll'), '--urls', `${ipv4};${ipv6}`
  ], {
    cwd: project,
    windowsHide: true,
    env: {
      ...process.env,
      ASPNETCORE_ENVIRONMENT: 'Development',
      AccessGuard__DataPath: data,
      AccessGuard__BruteForce__MaxFailures: '3',
      AccessGuard__BruteForce__WindowSeconds: '30',
      AccessGuard__BruteForce__BlockSeconds: '10'
    }
  });
  server.on('error', error => { logs += error.message; });
  for (const stream of [server.stdout, server.stderr]) stream.on('data', chunk => { logs += chunk; });
  let lastError;
  for (let step = 0; step < 60; step++) {
    await delay(200);
    try {
      const response = await fetch(ipv4 + '/api/auth/session');
      if (response.ok) return;
      lastError = new Error(`Readiness endpoint returned HTTP ${response.status}.`);
    } catch (error) {
      lastError = error.cause || error;
    }
    if (server.exitCode !== null) break;
  }
  throw new Error('AccessGuard failed to start.', { cause: lastError });
}

async function stop() {
  if (!server || server.exitCode !== null || server.signalCode !== null || !server.pid) return;
  await new Promise(resolve => { server.once('exit', resolve); server.kill(); });
}

(async () => {
  try {
    await start();
    const admin = client(ipv6);
    const source = client(ipv4);
    await admin('/api/auth/session');
    assert.equal((await admin('/api/auth/bootstrap', { ...account, name: 'Admin Test' })).status, 200);
    assert.equal((await admin('/api/auth/login', account)).status, 200);
    await admin('/api/auth/session');
    await source('/api/auth/session');

    const rejected = { email: 'fixture@accessguard.test', password: 'Invalid-Fixture!123' };
    assert.equal((await source('/api/auth/login', rejected)).status, 401);
    const responses = await Promise.all(Array.from({ length: 4 }, () => source('/api/auth/login', rejected)));
    assert(responses.every(response => [401, 429].includes(response.status)));
    assert(responses.some(response => response.status === 429));

    const alerts = await (await admin('/api/audit?query=alerte+brute+force')).json();
    assert.equal(alerts.total, 1);
    assert.equal(alerts.logs[0].ip, '127.0.0.1');
    assert.equal(alerts.logs[0].result, 'Bloqué');
    assert.match(await (await admin('/api/audit/export?query=127.0.0.1')).text(), /"127\.0\.0\.1"/);
    assert.equal((await (await admin('/api/workspace')).json()).meta.bruteForceAlerts, 1);

    const otherIp = client(ipv6);
    await otherIp('/api/auth/session');
    assert.equal((await otherIp('/api/auth/login', account)).status, 200);
    assert.equal((await source('/api/auth/twofactor', { code: '123456' })).status, 429);

    await stop();
    await start();
    await source('/api/auth/session');
    const blocked = await source('/api/auth/login', account);
    assert.equal(blocked.status, 429);
    const retryAfter = Number(blocked.headers.get('Retry-After'));
    assert(retryAfter > 0 && retryAfter <= 10);
    await delay((retryAfter + 0.2) * 1000);
    assert.equal((await source('/api/auth/login', rejected)).status, 401);
    assert.equal((await source('/api/auth/login', account)).status, 200);
    console.log('PASS: audit alert, concurrent failures, IP isolation, 2FA, persistence and expiration.');
  } finally {
    await stop();
  }
})().catch(error => { console.error(error, '\n', logs.slice(-5000)); process.exitCode = 1; });
