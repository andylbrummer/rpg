import { test, expect } from './fixtures';
import { getGameState, resetGame } from './helpers';

test.describe('G3: Characters', () => {
  test.beforeEach(async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await resetGame(page, serverUrl);
  });

  test('party members in initial state', async ({ page }) => {
    const state = await getGameState(page);
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

  test('party members have HP and maxHP', async ({ page }) => {
    const state = await getGameState(page);
    expect(state.party.length).toBe(6);

    for (const member of state.party) {
      expect(member.hp).toBeGreaterThan(0);
      expect(member.maxHp).toBeGreaterThan(0);
      expect(member.hp).toBeLessThanOrEqual(member.maxHp);
    }
  });

  test('character classes and rows assigned', async ({ page }) => {
    const state = await getGameState(page);
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
