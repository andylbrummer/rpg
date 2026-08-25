import { test, expect, type Page } from '@playwright/test';
import { loadTown, MENU_STATE } from './townHarness';

/**
 * Naming the faction behind the scheme is the campaign's central decision — seven pieces of
 * evidence make the case public, there is exactly one accusation per campaign, and naming the
 * wrong faction costs standing and hands the real mastermind the advantage. The command existed,
 * was handled and was tested server-side, and the state frame even carried a canAccuse flag, but
 * nothing in the client read it or sent the action: the campaign could be investigated and never
 * concluded.
 */

async function installActionCapture(page: Page) {
  await page.evaluate(() => {
    const client = (window as any).gameClient;
    const captured: unknown[] = [];
    (window as any).__capturedActions = captured;
    client.sendAction = (action: unknown) => {
      captured.push(action);
    };
  });
}

async function getCaptured(page: Page): Promise<any[]> {
  return page.evaluate(() => (window as any).__capturedActions ?? []);
}

async function setState(page: Page, state: Record<string, unknown>) {
  await page.evaluate((s) => {
    (window as any).__rpc_enableTestHooks();
    (window as any).gameStore.__testSetState(s);
  }, state);
}

async function openMissions(page: Page) {
  await page.locator('.town-nav-btn').filter({ hasText: 'Missions' }).click();
}

const NO_EVIDENCE = {
  canConfront: false,
  canAccuse: false,
  hasIrrefutableProof: false,
  canBetray: false,
  onBetrayalPath: false,
};

test.describe('Accusation', () => {
  test('is hidden until the case is strong enough', async ({ page }) => {
    await loadTown(page);
    await setState(page, {
      ...MENU_STATE,
      evidence: { ...NO_EVIDENCE, counters: { bureau: 6 } },
    });
    await openMissions(page);

    await expect(page.getByTestId('accusation-offer')).toHaveCount(0);
  });

  test('offers only the factions the evidence reaches, and sends the chosen one', async ({ page }) => {
    await loadTown(page);
    await installActionCapture(page);
    await setState(page, {
      ...MENU_STATE,
      evidence: { ...NO_EVIDENCE, canAccuse: true, counters: { bureau: 8, convocation: 3 } },
    });
    await openMissions(page);

    await expect(page.getByTestId('accusation-offer')).toHaveCount(1);
    await page.locator('[data-testid="accuse-btn"][data-faction="bureau"]').click();

    expect(await getCaptured(page)).toContainEqual({ type: 'accuse_faction', targetId: 'bureau' });
  });

  test('a campaign that has already accused shows the naming, not another offer', async ({ page }) => {
    await loadTown(page);
    await setState(page, {
      ...MENU_STATE,
      evidence: { ...NO_EVIDENCE, canAccuse: true, counters: { bureau: 8 }, accusedFaction: 'bureau' },
    });
    await openMissions(page);

    await expect(page.locator('.accusation-made')).toBeVisible();
    await expect(page.getByTestId('accusation-offer')).toHaveCount(0);
  });
});
