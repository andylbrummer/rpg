import { test, expect } from './fixtures';

test.describe('G1: Skeleton', () => {
  test('serves frontend with correct title', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    expect(await page.title()).toBe('The Reach');
  });

  test('serves javascript bundle', async ({ request, serverUrl }) => {
    // Find the main JS bundle dynamically
    const pageRes = await request.get(`${serverUrl}/app`);
    const html = await pageRes.text();
    const match = html.match(/src="\/assets\/([^"]+\.js)"/);
    expect(match).toBeTruthy();
    const jsRes = await request.get(`${serverUrl}/assets/${match![1]}`);
    expect(jsRes.status()).toBe(200);
    expect(jsRes.headers()['content-type']).toContain('javascript');
  });

  test('websocket connects and broadcasts state', async ({ page, serverUrl }) => {
    const captured = await page.evaluate((url) => {
      return new Promise<any[]>((resolve) => {
        const msgs: any[] = [];
        const ws = new WebSocket(`ws://${new URL(url).host}/`);
        ws.onmessage = (e) => {
          msgs.push(JSON.parse(e.data));
          ws.close();
          resolve(msgs);
        };
        ws.onerror = () => { ws.close(); resolve([]); };
        setTimeout(() => { ws.close(); resolve(msgs); }, 3000);
      });
    }, serverUrl);

    expect(captured.length).toBeGreaterThan(0);
    expect(captured[0].type).toBe('state');
    expect(captured[0].mode).toBeDefined();
  });
});
