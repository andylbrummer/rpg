import { test, expect } from './fixtures';

async function getState(page: any, serverUrl: string): Promise<any> {
  return page.evaluate((url: string) => {
    return new Promise<any>((resolve) => {
      let clientSeq = 1;
      const ws = new WebSocket(`ws://${new URL(url).host}/`);
      ws.onmessage = (e) => {
        const envelope = JSON.parse(e.data);
        if (envelope.type === 'hello') {
          ws.send(JSON.stringify({ v: 2, type: 'ready', seq: clientSeq++, payload: {} }));
        } else if (envelope.type === 'state') {
          ws.close();
          resolve(envelope.payload);
        }
      };
      ws.onerror = () => { ws.close(); resolve(null); };
      setTimeout(() => { ws.close(); resolve(null); }, 3000);
    });
  }, serverUrl);
}

test.describe('G3: Characters', () => {
  test.beforeEach(async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await page.waitForTimeout(500);
  });

  test('party members in initial state', async ({ page, serverUrl }) => {
    const state = await getState(page, serverUrl);
    expect(state).not.toBeNull();
    expect(state.party).toBeDefined();
    expect(state.party.length).toBe(6);

    const names = state.party.map((m: any) => m.name);
    expect(names).toContain('Kael');
    expect(names).toContain('Sera');
    expect(names).toContain('Mira');
    expect(names).toContain('Vex');
    expect(names).toContain('Nyx');
    expect(names).toContain('Orin');
  });

  test('party members have HP and maxHP', async ({ page, serverUrl }) => {
    const state = await getState(page, serverUrl);
    expect(state.party.length).toBe(6);

    for (const member of state.party) {
      expect(member.hp).toBeGreaterThan(0);
      expect(member.maxHp).toBeGreaterThan(0);
      expect(member.hp).toBeLessThanOrEqual(member.maxHp);
    }
  });

  test('character classes and rows assigned', async ({ page, serverUrl }) => {
    const state = await getState(page, serverUrl);
    const classes = state.party.map((m: any) => m.classId);
    expect(classes).toContain('bonewarden');
    expect(classes).toContain('stillblade');
    expect(classes).toContain('cauterist');
    expect(classes).toContain('hollow');

    const rows = state.party.map((m: any) => m.row);
    expect(rows.filter((r: number) => r === 0).length).toBe(3); // 3 front
    expect(rows.filter((r: number) => r === 1).length).toBe(3); // 3 back
  });
});
