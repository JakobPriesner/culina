import { expect, test, type Page } from '@playwright/test';

import {
  accountFor,
  needsBackend,
  seedRecipe,
  signInWithHousehold,
  skipReason,
  unique
} from './support/culina';

/**
 * Sharing with somebody without Culina: opening the sheet is the decision to share, and reading the
 * link back gives the same one.
 */
test.describe.configure({ mode: 'serial' });

test.describe('sharing', () => {
  test.skip(needsBackend, skipReason);

  let page: Page;
  let title: string;
  let recipeId: string;

  test.beforeAll(async ({ browser }, testInfo) => {
    if (needsBackend) {
      return;
    }

    page = await browser.newPage();

    await signInWithHousehold(page, await accountFor(browser, testInfo));

    title = unique('Shakshuka');
    recipeId = await seedRecipe(page, { title, yieldAmount: 2 });
  });

  test.afterAll(async () => {
    await page?.close();
  });

  async function openShareSheet() {
    await page.goto(`/recipes/${recipeId}`);
    await expect(page.getByRole('heading', { level: 1, name: title })).toBeVisible();

    await page.getByRole('button', { name: /more actions|weitere aktionen/i }).click();
    await page.getByRole('button', { name: /^(share|teilen)$/i }).click();
  }

  test('opening the sheet makes the link, without another tap', async () => {
    await openShareSheet();

    await expect(page.getByTestId('share-link')).toHaveText(/\/shared\/.+/);
    await expect(page.getByRole('button', { name: /make a link|link erstellen/i })).toBeHidden();
  });

  test('opening it again reads back the same link', async () => {
    const first = await page.getByTestId('share-link').textContent();

    await openShareSheet();

    await expect(page.getByTestId('share-link')).toHaveText(first ?? '');
  });

  test('taking it back leaves the sheet offering a new one', async () => {
    await page.getByRole('button', { name: /stop sharing|freigabe beenden/i }).click();

    await expect(page.getByTestId('share-link')).toBeHidden();
    await expect(page.getByRole('button', { name: /make a link|link erstellen/i })).toBeVisible();
  });
});
