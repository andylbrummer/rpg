import { test, expect } from './fixtures';
import { getMainJsUrl } from './helpers';

test.describe('Smoke', () => {
  test('serves the frontend app', async ({ page }) => {
    const res = await page.goto('/app');
    expect(res?.status()).toBe(200);
    await expect(page).toHaveTitle('The Reach');
  });

  test('serves static assets', async ({ request }) => {
    const jsName = await getMainJsUrl(request, '');
    const res = await request.get(`/assets/${jsName}`);
    expect(res.status()).toBe(200);
    const ct = res.headers()['content-type'] || '';
    expect(ct).toContain('javascript');
  });

  test('websocket endpoint is reachable', async ({ page }) => {
    await page.goto('/app');
    const wsState = await page.evaluate(() => new Promise((resolve) => {
      const ws = new WebSocket(`ws://${location.host}/`);
      ws.onopen = () => { ws.close(); resolve('open'); };
      ws.onerror = () => resolve('error');
    }));
    expect(wsState).toBe('open');
  });
});
