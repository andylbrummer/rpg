import type { Page, APIRequestContext } from '@playwright/test';

export async function sendWsAction(page: Page, _serverUrl: string, action: any): Promise<void> {
  await page.evaluate((act: any) => {
    (window as any).gameClient?.sendAction(act);
  }, action);
  await page.waitForTimeout(600);
}

export async function getPositionText(page: Page): Promise<string> {
  const el = page.locator('.exploration-hud .position');
  try {
    await el.waitFor({ timeout: 10000 });
    return await el.textContent() ?? '';
  } catch {
    return '';
  }
}

export async function getMainJsUrl(request: APIRequestContext, base: string): Promise<string> {
  const pageRes = await request.get(`${base || ''}/app`);
  const html = await pageRes.text();
  const match = html.match(/src="\/assets\/([^"]+\.js)"/);
  if (!match) throw new Error('Could not find main JS bundle');
  return match[1];
}
