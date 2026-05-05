import { test, expect } from '@playwright/test';
import { spawn, ChildProcess } from 'child_process';
import { resolve } from 'path';

let serverProcess: ChildProcess;

async function waitForServer(url: string, timeout = 15000): Promise<void> {
  const start = Date.now();
  while (Date.now() - start < timeout) {
    try {
      const res = await fetch(url);
      if (res.status === 200) return;
    } catch {
      // not ready yet
    }
    await new Promise(r => setTimeout(r, 200));
  }
  throw new Error('Server did not start in time');
}

test.beforeAll(async () => {
  const hostDll = resolve(__dirname, '../../../src/engine/RPC.Host/bin/Release/net9.0/RPC.Host.dll');
  serverProcess = spawn('/home/beagle/.dotnet/dotnet', [hostDll, '--headless'], {
    cwd: resolve(__dirname, '../../../src/engine'),
    stdio: 'pipe',
  });

  await waitForServer('http://localhost:19421/app');
});

test.afterAll(() => {
  if (serverProcess) {
    serverProcess.kill();
  }
});

test('serves the frontend app', async ({ page }) => {
  const res = await page.goto('/app');
  expect(res?.status()).toBe(200);
  expect(await page.title()).toBe('The Reach');
});

test('serves static assets', async ({ request }) => {
  const res = await request.get('/assets/index-nrexz-U7.js');
  expect(res.status()).toBe(200);
  expect(res.headers()['content-type']).toContain('javascript');
});

test('websocket endpoint is reachable', async ({ page }) => {
  const wsMessages: string[] = [];
  const ws = new WebSocket('ws://localhost:19421/');
  
  await new Promise<void>((resolve, reject) => {
    const timer = setTimeout(() => reject(new Error('WS timeout')), 5000);
    ws.onopen = () => {
      clearTimeout(timer);
      resolve();
    };
    ws.onerror = () => {
      clearTimeout(timer);
      reject(new Error('WS connection failed'));
    };
  });

  ws.onmessage = (e) => wsMessages.push(e.data);
  await page.waitForTimeout(500);
  ws.close();

  expect(wsMessages.length).toBeGreaterThan(0);
  const firstMessage = JSON.parse(wsMessages[0]);
  expect(firstMessage.type).toBe('state');
});
