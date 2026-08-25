import { test, expect } from './fixtures';
import { getMainJsUrl } from './helpers';

test.describe('Smoke', () => {
  test('serves the frontend app', async ({ page, serverUrl }) => {
    const res = await page.goto(`${serverUrl}/app`);
    expect(res?.status()).toBe(200);
    await expect(page).toHaveTitle('The Reach');
  });

  test('serves static assets', async ({ request, serverUrl }) => {
    const jsName = await getMainJsUrl(request, serverUrl);
    const res = await request.get(`${serverUrl}/assets/${jsName}`);
    expect(res.status()).toBe(200);
    const ct = res.headers()['content-type'] || '';
    expect(ct).toContain('javascript');
  });

  test('websocket endpoint is reachable', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    const wsState = await page.evaluate(() => new Promise((resolve) => {
      const ws = new WebSocket(`ws://${location.host}/`);
      ws.onopen = () => { ws.close(); resolve('open'); };
      ws.onerror = () => resolve('error');
    }));
    expect(wsState).toBe('open');
  });
});
