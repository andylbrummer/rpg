import { test as base } from '@playwright/test';
import { spawn, ChildProcess } from 'child_process';
import { resolve } from 'path';
import { rmSync } from 'fs';
import { homedir } from 'os';

export type ServerFixture = {
  serverUrl: string;
};

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

export const test = base.extend<ServerFixture>({
  serverUrl: [async ({}, use) => {
    // Clean up persistent save file to prevent turn-count accumulation across test runs
    try {
      rmSync(resolve(homedir(), '.local/share/TheReach/save.json'));
    } catch {
      // ignore if file does not exist
    }

    const hostDll = resolve(__dirname, '../../../src/engine/RPC.Host/bin/Release/net9.0/RPC.Host.dll');
    const proc = spawn('dotnet', [hostDll, '--headless'], {
      cwd: resolve(__dirname, '../../../src/engine'),
      stdio: 'pipe',
    });

    const url = 'http://localhost:19421';
    await waitForServer(`${url}/app`);
    await use(url);
    proc.kill();
  }, { scope: 'worker' }],
});

export { expect } from '@playwright/test';
