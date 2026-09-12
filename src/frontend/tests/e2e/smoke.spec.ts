import { expect, test } from '@playwright/test';

/**
 * The one test that must never be skipped: it proves the production build
 * boots and is reachable at `/`. Everything else can be wrong; if this fails,
 * nothing else is worth reading.
 */
test('the built app boots without throwing @offline', async ({ page }) => {
  const failures: string[] = [];

  page.on('pageerror', (error) => failures.push(error.message));

  await page.goto('/');

  await expect(page.getByRole('heading', { level: 1 })).toBeVisible();

  expect(failures, 'the page threw while booting').toEqual([]);
});
