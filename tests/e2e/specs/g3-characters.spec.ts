import { test, expect } from './fixtures';

async function getState(page: any, serverUrl: string): Promise<any> {
  return page.evaluate((url: string) => {
    return new Promise<any>((resolve) => {
      const ws = new WebSocket(`ws://${new URL(url).host}/`);
      ws.onmessage = (e) => {
        const data = JSON.parse(e.data);
        ws.close();
        resolve(data);
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
    expect(state.party.length).toBe(4);

    const names = state.party.map((m: any) => m.name);
    expect(names).toContain('Kael');
    expect(names).toContain('Sera');
    expect(names).toContain('Mira');
    expect(names).toContain('Vex');
  });

  test('party members have HP and maxHP', async ({ page, serverUrl }) => {
    const state = await getState(page, serverUrl);
    expect(state.party.length).toBe(4);

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
    expect(rows.filter((r: number) => r === 0).length).toBe(2); // 2 front
    expect(rows.filter((r: number) => r === 1).length).toBe(2); // 2 back
  });
});
