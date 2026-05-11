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
        let clientSeq = 1;
        const ws = new WebSocket(`ws://${new URL(url).host}/`);
        ws.onmessage = (e) => {
          const envelope = JSON.parse(e.data);
          msgs.push(envelope);
          if (envelope.type === 'hello') {
            ws.send(JSON.stringify({ v: 2, type: 'ready', seq: clientSeq++, payload: {} }));
          } else if (envelope.type === 'state') {
            ws.close();
            resolve(msgs);
          }
        };
        ws.onerror = () => { ws.close(); resolve([]); };
        setTimeout(() => { ws.close(); resolve(msgs); }, 3000);
      });
    }, serverUrl);

    expect(captured.length).toBeGreaterThanOrEqual(2);
    expect(captured[0].type).toBe('hello');
    expect(captured[1].type).toBe('state');
    expect(captured[1].payload.mode).toBeDefined();
  });
});
