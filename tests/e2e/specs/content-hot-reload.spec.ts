import { test, expect } from '@playwright/test';
import { spawn, ChildProcess } from 'child_process';
import { resolve } from 'path';
import { readFileSync, writeFileSync } from 'fs';
import { createServer } from 'net';

function getFreePort(): Promise<number> {
  return new Promise((resolve, reject) => {
    const server = createServer();
    server.listen(0, () => {
      const port = (server.address() as any).port;
      server.close(() => resolve(port));
    });
    server.on('error', reject);
  });
}

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

test.describe('Content Hot Reload', () => {
  test('touching segment JSON broadcasts content.reload over websocket', async ({ page }) => {
    const port = await getFreePort();
    const hostDll = resolve(__dirname, '../../../src/engine/RPC.Host/bin/Debug/net9.0/RPC.Host.dll');
    const proc = spawn('dotnet', [hostDll, '--headless', '--dev', `--port=${port}`], {
      cwd: resolve(__dirname, '../../../src/engine'),
      stdio: 'pipe',
    });

    const url = `http://localhost:${port}`;
    await waitForServer(`${url}/app`);

    try {
      const wsUrl = `ws://localhost:${port}/`;

      // Connect via WebSocket in page context and wait for content.reload
      const reloadPromise = page.evaluate((wsEndpoint) => new Promise<string>((resolve, reject) => {
        const ws = new WebSocket(wsEndpoint);
        let resolved = false;
        let readySent = false;

        ws.onopen = () => {
          // hello will be sent by server; we respond with ready
        };

        ws.onmessage = (event) => {
          const msg = JSON.parse(event.data);
          if (msg.type === 'hello' && !readySent) {
            readySent = true;
            ws.send(JSON.stringify({ v: 2, type: 'ready', seq: 1, payload: {} }));
            return;
          }
          if (msg.type === 'content.reload') {
            if (!resolved) {
              resolved = true;
              resolve(JSON.stringify(msg));
              ws.close();
            }
          }
        };

        ws.onerror = () => {
          if (!resolved) {
            resolved = true;
            reject(new Error('WebSocket error'));
          }
        };

        setTimeout(() => {
          if (!resolved) {
            resolved = true;
            reject(new Error('Timeout waiting for content.reload'));
            ws.close();
          }
        }, 8000);
      }), wsUrl);

      // Wait for WS handshake and ready to be sent
      await page.waitForTimeout(800);

      // Touch a segment JSON file to trigger file watcher
      const segmentPath = resolve(__dirname, '../../../content/segments/broken-engine/entrance.json');
      const original = readFileSync(segmentPath, 'utf-8');
      writeFileSync(segmentPath, original + '\n');

      // Restore original content after a brief delay
      setTimeout(() => {
        writeFileSync(segmentPath, original);
      }, 1000);

      const result = await reloadPromise;
      const msg = JSON.parse(result);
      expect(msg.type).toBe('content.reload');
      expect(msg.payload.category).toBe('segments');
    } finally {
      proc.kill();
    }
  });
});
