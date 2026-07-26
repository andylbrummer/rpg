import { test, expect } from './fixtures';

function makeMockCombat(partyCount: number, enemyCount: number) {
  const combatants = [
    ...Array.from({ length: partyCount }, (_, i) => ({
      id: `p${i}`,
      name: `Hero${i + 1}`,
      isPlayer: true,
      classId: 'warrior',
      hp: 100,
      maxHp: 100,
      speed: 5,
      row: i < 3 ? 0 : 1,
      alive: true,
      isCurrent: i === 0,
      abilities: [],
    })),
    ...Array.from({ length: enemyCount }, (_, i) => ({
      id: `e${i}`,
      name: `Enemy${i + 1}`,
      isPlayer: false,
      hp: 50,
      maxHp: 50,
      speed: 4,
      row: i < 3 ? 0 : 1,
      alive: true,
      isCurrent: false,
      abilities: [],
    })),
  ];
  const initiativeOrder = combatants.map(c => c.id);
  return {
    phase: 'Turn',
    round: 1,
    combatants,
    initiativeOrder,
    currentTurnIndex: 0,
    log: [],
    isFinished: false,
  };
}

async function injectGameState(page: any, state: any) {
  // Reset action-log turn tracker so injected low-turn entries are processed
  await page.evaluate(() => {
    const store = (window as any).gameStore;
    store.__testSetState({ type: 'state', actionLog: [] });
  });
  await page.evaluate((s: any) => {
    const store = (window as any).gameStore;
    store.__testSetState(s);
  }, state);
}

test.describe('Synergy Feedback', () => {
  test('synergy trigger shows 500ms flash on target', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await page.evaluate(() => {
      localStorage.removeItem('rpc_discovered_synergies');
      localStorage.removeItem('rpc_revealed_synergies');
    });
    await page.reload();
    await page.waitForTimeout(500);

    const combat = makeMockCombat(2, 2);
    const baseState = {
      type: 'state',
      mode: 'Combat',
      player: { x: 0, y: 0, facing: 'North' },
      tiles: [],
      explored: [],
      hasDungeon: true,
      party: [],
      combat,
    };

    await injectGameState(page, {
      ...baseState,
      actionLog: []
    });

    await expect(page.locator('.combat-overlay')).toBeVisible();

    await page.evaluate((s: any) => {
      const store = (window as any).gameStore;
      store.__testSetState(s);
    }, {
      ...baseState,
      actionLog: [
        { turn: 100, category: 'combat', type: 'encounter_started', payload: { encounterId: 'enc-1' } },
        { turn: 101, category: 'combat', type: 'synergy_triggered', payload: { synergyId: 'stillblade_hollow_smoke_silence', encounterId: 'enc-1', targetId: 'e0' } }
      ]
    });

    // Poll briefly for the flash to appear (avoids race with Svelte effect scheduling)
    let flashInfo: { flashTarget: string; hasFlashClass: boolean; flashParentText: string } | null = null;
    for (let i = 0; i < 20; i++) {
      flashInfo = await page.evaluate(() => {
        const overlay = document.querySelector('[data-testid="combat-overlay"]');
        const flashEl = document.querySelector('.synergy-flash');
        return {
          flashTarget: overlay?.getAttribute('data-flash-target') ?? 'missing',
          hasFlashClass: flashEl !== null,
          // The side's section title; h2 since the combat overlay gained an h1 of its own.
          flashParentText: flashEl?.closest('.enemy-side, .player-side')?.querySelector('h2')?.textContent ?? 'unknown',
        };
      });
      if (flashInfo.hasFlashClass) break;
      await page.waitForTimeout(25);
    }
    expect(flashInfo!.flashTarget).toBe('e0');
    expect(flashInfo!.hasFlashClass).toBe(true);
    expect(flashInfo!.flashParentText).toBe('Enemies');

    // Wait for 500ms flash to end, then verify it's gone
    await page.waitForTimeout(600);
    const hasFlashAfter = await page.evaluate(() => document.querySelector('.synergy-flash') !== null);
    expect(hasFlashAfter).toBe(false);
  });

  test('field notes reveals entry post-combat not during', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await page.evaluate(() => {
      localStorage.removeItem('rpc_discovered_synergies');
      localStorage.removeItem('rpc_revealed_synergies');
    });
    await page.reload();
    await page.waitForTimeout(500);

    const combat = makeMockCombat(2, 2);
    await injectGameState(page, {
      type: 'state',
      mode: 'Combat',
      player: { x: 0, y: 0, facing: 'North' },
      tiles: [],
      explored: [],
      hasDungeon: true,
      party: [],
      combat,
      actionLog: [
        { turn: 1, category: 'combat', type: 'encounter_started', payload: { encounterId: 'enc-1' } },
        { turn: 2, category: 'combat', type: 'synergy_triggered', payload: { synergyId: 'stillblade_hollow_smoke_silence', encounterId: 'enc-1', targetId: 'e0' } }
      ]
    });

    // Field Notes button should not be visible during combat
    await expect(page.locator('.field-notes-toggle')).not.toBeVisible();

    // End combat by switching to exploration
    await injectGameState(page, {
      type: 'state',
      mode: 'Exploration',
      player: { x: 0, y: 0, facing: 'North' },
      tiles: [],
      explored: [],
      hasDungeon: true,
      party: [],
      actionLog: [
        { turn: 100, category: 'combat', type: 'encounter_started', payload: { encounterId: 'enc-1' } },
        { turn: 101, category: 'combat', type: 'synergy_triggered', payload: { synergyId: 'stillblade_hollow_backstep', encounterId: 'enc-1', targetId: 'e0' } },
        { turn: 102, category: 'combat', type: 'encounter_won', payload: { encounterId: 'enc-1' } }
      ]
    });

    // Field Notes button should now be visible
    await expect(page.locator('.field-notes-toggle')).toBeVisible();
    await page.locator('.field-notes-toggle').click();

    // The discovered synergy should now be revealed
    await expect(page.locator('.field-note-entry .field-note-names', { hasText: 'silence_strike + smoke_bomb' })).toBeVisible();

    // Undiscovered synergies should still show ???. The total comes from synergy content and
    // moves whenever content is added, so derive it rather than pinning today's count: what
    // this test owns is that exactly the one discovered entry is revealed.
    const total = await page.locator('.field-note-entry').count();
    expect(total).toBeGreaterThan(1);
    await expect(page.locator('.field-note-entry .field-note-names', { hasText: '??? + ???' })).toHaveCount(total - 1);
  });

  test('replay modal opens and shows animation', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await page.evaluate(() => {
      localStorage.removeItem('rpc_discovered_synergies');
      localStorage.removeItem('rpc_revealed_synergies');
    });
    await page.reload();
    await page.waitForTimeout(500);

    // Pre-seed discovered and revealed synergies
    await page.evaluate(() => {
      localStorage.setItem('rpc_discovered_synergies', JSON.stringify(['stillblade_hollow_smoke_silence']));
      localStorage.setItem('rpc_revealed_synergies', JSON.stringify(['stillblade_hollow_smoke_silence']));
    });

    // Reload so the app picks up localStorage
    await page.reload();
    await page.waitForTimeout(500);

    await injectGameState(page, {
      type: 'state',
      mode: 'Menu',
      player: { x: 0, y: 0, facing: 'North' },
      tiles: [],
      explored: [],
      hasDungeon: false,
      party: [],
    });

    await page.waitForSelector('.field-notes-toggle');

    await page.locator('.field-notes-toggle').click();
    await page.locator('.replay-btn').first().click();

    // Verify modal and animation are visible
    await expect(page.locator('.replay-modal-overlay')).toBeVisible();
    await expect(page.locator('.replay-anim')).toBeVisible();

    // Close modal
    await page.locator('.replay-close-btn').click();
    await expect(page.locator('.replay-modal-overlay')).not.toBeVisible();
  });
});
