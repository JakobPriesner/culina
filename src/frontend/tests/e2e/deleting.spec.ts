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
 * Deleting a recipe.
 *
 * The one thing Culina asks about first, because there is no undo behind it:
 * the recipe's cooking history, notes and planned meals go with it. So the
 * question has to be there, the safe answer has to keep the recipe, and the
 * other answer has to leave it genuinely gone rather than hidden.
 */
test.describe.configure({ mode: 'serial' });

test.describe('deleting a recipe', () => {
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

    title = unique('Ratatouille');
    recipeId = await seedRecipe(page, { title });
  });

  test.afterAll(async () => {
    await page?.close();
  });

  async function askToDelete() {
    await page.goto(`/recipes/${recipeId}`);
    await expect(page.getByRole('heading', { level: 1, name: title })).toBeVisible();

    await page.getByRole('button', { name: /more actions|weitere aktionen/i }).click();
    await page.getByRole('button', { name: /^(delete recipe|rezept löschen)$/i }).click();

    return page.getByRole('dialog');
  }

  test('asks first, and keeping it keeps it', async () => {
    const dialog = await askToDelete();

    await expect(dialog).toContainText(/can't be undone|nicht rückgängig/i);

    await dialog.getByRole('button', { name: /^(keep it|behalten)$/i }).click();

    await expect(dialog).toBeHidden();
    await expect(page.getByRole('heading', { level: 1, name: title })).toBeVisible();
    expect((await page.request.get(`/api/v1/recipes/${recipeId}`)).status()).toBe(200);
  });

  test('deleting it goes back to the library, without it', async () => {
    const dialog = await askToDelete();

    await dialog.getByRole('button', { name: /^(delete|löschen)$/i }).click();

    await expect(page).toHaveURL(/\/$/);
    await expect(page.getByText(/was deleted|wurde gelöscht/i)).toContainText(title);
    await expect(page.getByRole('link', { name: new RegExp(title) })).toBeHidden();
  });

  test('is gone from the server, not only from the screen', async () => {
    expect((await page.request.get(`/api/v1/recipes/${recipeId}`)).status()).toBe(404);
  });
});
