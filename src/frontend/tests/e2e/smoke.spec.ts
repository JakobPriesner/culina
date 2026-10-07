import { expect, test } from '@playwright/test';

/** Must never be skipped: proves the production build boots and is reachable at `/`. */
test('the built app boots without throwing @offline', async ({ page }) => {
  const failures: string[] = [];

  page.on('pageerror', (error) => failures.push(error.message));

  // Unauthenticated, so this lands on sign-in, which must render for anyone.
  await page.goto('/');

  await expect(page.getByRole('heading', { level: 1 })).toBeVisible();

  expect(failures, 'the page threw while booting').toEqual([]);
});
