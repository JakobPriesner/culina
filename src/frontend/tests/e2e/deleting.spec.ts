import { expect, test, type Page } from '@playwright/test';

import {
  accountFor,
  needsBackend,
  seedRecipe,
  signInWithHousehold,
  skipReason,
  unique,
  writeHeaders
} from './support/culina';

/** Deleting a recipe, which has no undo behind the question: the safe answer keeps it, the other leaves it gone, not hidden. */
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

    await page.getByRole('button', { name: /^(delete recipe|rezept löschen)$/i }).click();

    return page.getByRole('dialog');
  }

  test('asks first, and keeping it keeps it', async () => {
    const dialog = await askToDelete();

    // Says how to get it back (30 days).
    await expect(dialog).toContainText(/still restore it|noch wiederherstellen/i);

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

  test('takes the "now cooking" bar with it when it was being cooked', async () => {
    title = unique('Gazpacho');
    recipeId = await seedRecipe(page, { title });

    const started = await page.request.post('/api/v1/cook-sessions', {
      headers: await writeHeaders(page),
      data: { recipeId, servings: 2 }
    });

    expect(started.ok(), await started.text()).toBe(true);

    const bar = page.getByRole('link', { name: /keep cooking|weiterkochen/i });
    const dialog = await askToDelete();

    await expect(bar).toBeVisible();

    await dialog.getByRole('button', { name: /^(delete|löschen)$/i }).click();

    await expect(page).toHaveURL(/\/$/);
    await expect(bar).toBeHidden();

    // The server ended the session with the recipe, so a fresh page offers nothing.
    await page.reload();
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
    await expect(bar).toBeHidden();
  });
});
