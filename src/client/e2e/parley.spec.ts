import { test, expect, type Page } from '@playwright/test';
import { loadTown, MENU_STATE } from './townHarness';

/**
 * A faction encounter the party can talk its way out of. The engine pauses the encounter, offers
 * the options this party has earned, and waits for an `encounter_choice` — but nothing in the
 * client read the offer or sent the answer, so walking into a patrol at good standing did nothing
 * at all: no fight, no parley, no Ashmouth negotiation, no Bonewarden ancestral bargain. The
 * command existed, was handled and was tested server-side; only the way in was missing.
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

test.describe('Faction parley', () => {
  test('no offer, no card', async ({ page }) => {
    await loadTown(page);
    await setState(page, { ...MENU_STATE, pendingParley: null });

    await expect(page.getByTestId('parley-card')).toHaveCount(0);
  });

  test('the offered options appear and the chosen one is sent', async ({ page }) => {
    await loadTown(page);
    await installActionCapture(page);
    await setState(page, {
      ...MENU_STATE,
      pendingParley: {
        encounterId: 'enc-1',
        factionId: 'inkblood',
        options: ['Parley', 'AncestralBargain', 'Fight'],
      },
    });

    await expect(page.getByTestId('parley-card')).toBeVisible();
    await expect(page.getByTestId('parley-option')).toHaveCount(3);

    await page.locator('[data-testid="parley-option"][data-option="AncestralBargain"]').click();

    expect(await getCaptured(page)).toContainEqual({
      type: 'encounter_choice',
      targetId: 'AncestralBargain',
    });
  });
});
