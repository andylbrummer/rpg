import { test as base } from '@playwright/test';
import { spawn, ChildProcess } from 'child_process';
import { resolve } from 'path';
import { rmSync } from 'fs';
import { homedir } from 'os';

export type ServerFixture = {
  serverUrl: string;
};

async function waitForServer(url: string, timeout = 60000): Promise<void> {
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
  /**
   * Render at half resolution for the whole suite.
   *
   * Headless Chromium has no GPU, so Three.js runs on SwiftShader and the render loop saturates
   * the page's main thread. Measured in a 1920x1080 test: a trivial page.evaluate(() => 1) took
   * 0.3-3.4 SECONDS, which is what made the viewport tests look flaky — every state observation
   * queued behind a frame, so waits blew through even a 60s ceiling. Quartering the pixel count
   * gives that time back.
   *
   * resolutionScale only affects the WebGL framebuffer, never DOM layout, and these specs assert
   * on DOM geometry and game state rather than 3D fidelity.
   */
  page: async ({ page }, use) => {
    await page.addInitScript(() => {
      localStorage.setItem('rpc_display_settings', JSON.stringify({
        fov: 75, resolutionScale: 0.5, vsync: true, fullscreen: false,
      }));
    });
    await use(page);
  },

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
