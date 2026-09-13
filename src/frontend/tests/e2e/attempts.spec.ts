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
 * A photograph of your own attempt.
 *
 * "Made 7×" becomes seven pictures, which is a better record of a recipe than
 * any rating: it is what the thing actually looked like when you made it. The
 * recipe's own photograph belongs to the household and says what the dish is
 * supposed to look like; these are personal and say what happened.
 */
test.describe.configure({ mode: 'serial' });

/** A one-pixel PNG, which is a real image and weighs nothing. */
const png = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==',
  'base64'
);

test.describe('a photo of how yours turned out', () => {
  test.skip(needsBackend, skipReason);

  let page: Page;
  let recipeId: string;

  test.beforeAll(async ({ browser }, testInfo) => {
    if (needsBackend) {
      return;
    }

    page = await browser.newPage();

    await signInWithHousehold(page, await accountFor(browser, testInfo));

    recipeId = await seedRecipe(page, {
      title: unique('Attempted'),
      yieldAmount: 2,
      ingredients: [{ quantity: 200, unit: 'g', name: 'Butter' }],
      steps: ['Melt {0}.']
    });
  });

  test.afterAll(async () => {
    await page?.close();
  });

  test('hangs a picture on one attempt, and takes it off again', async () => {
    await page.goto(`/recipes/${recipeId}`);
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();

    // Nothing to photograph until it has been cooked: the strip hangs off the
    // log, and an empty log has no attempts to illustrate.
    await expect(page.getByRole('region', { name: /your attempts|deine versuche/i })).toBeHidden();

    // An attempt exists because somebody cooked it, so this goes the way a
    // person does: start, walk to the last step, say you made it.
    await page.getByRole('button', { name: /start cooking|kochen starten/i }).click();
    await page.getByRole('button', { name: /^(i made it|fertig gekocht)$/i }).click();

    // Waited for, not assumed: navigating the moment the button is pressed
    // races the write that creates the attempt this test is about.
    await expect(
      page.getByText(/added to your cooking history|zu deiner kochhistorie/i)
    ).toBeVisible();

    await page.goto(`/recipes/${recipeId}`);

    const strip = page.getByRole('region', { name: /your attempts|deine versuche/i });

    await expect(strip).toBeVisible();

    // The empty frame is the affordance — a separate "add" button would be a
    // control for a thing that is not on the screen.
    await page.getByRole('button', { name: /add a photo|foto vom/i }).click();
    await page
      .getByLabel(/choose a photo|foto auswählen/i)
      .setInputFiles({ name: 'dinner.png', mimeType: 'image/png', buffer: png });

    await expect(strip.getByRole('img')).toBeVisible();

    // Served from the entry that owns it, not from the recipe: it is one
    // person's picture of one Tuesday.
    await expect(strip.getByRole('img')).toHaveAttribute(
      'src',
      new RegExp(`/recipes/${recipeId}/cook-log/[0-9a-f-]+/photo`)
    );

    await page.getByRole('button', { name: /^(remove|entfernen)$/i }).click();

    await expect(strip.getByRole('img')).toBeHidden();
    // The entry stays: that you cooked it is still true.
    await expect(strip).toBeVisible();
  });
});
