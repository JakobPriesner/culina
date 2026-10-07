import { expect, test, type Page } from '@playwright/test';

import {
  accountFor,
  needsBackend,
  seedRecipe,
  signInWithHousehold,
  skipReason,
  unique
} from './support/culina';

/** A recipe on paper: no app chrome, black on white, and the scaled servings stated once. */
test.describe.configure({ mode: 'serial' });

test.describe('printing a recipe', () => {
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
      title: unique('Printed'),
      yieldAmount: 2,
      ingredients: [{ quantity: 200, unit: 'g', name: 'Butter' }],
      steps: ['Melt {0} in a wide pan.']
    });
  });

  test.afterAll(async () => {
    await page?.close();
  });

  test('leaves the app behind and prints only the recipe', async () => {
    await page.goto(`/recipes/${recipeId}`);
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();

    await page.emulateMedia({ media: 'print' });

    await expect(page.getByRole('region', { name: /ingredients|zutaten/i })).toContainText(
      'Butter'
    );
    await expect(page.getByRole('region', { name: /steps|zubereitung/i })).toContainText('Melt');

    await expect(page.getByRole('banner')).toBeHidden();
    await expect(page.getByRole('navigation').first()).toBeHidden();
    await expect(page.getByRole('link', { name: /skip to content|zum inhalt/i })).toBeHidden();

    await expect(page.getByRole('button', { name: /start cooking|kochen starten/i })).toBeHidden();

    const ground = await page.evaluate(() => getComputedStyle(document.body).backgroundColor);

    expect(ground).toBe('rgb(255, 255, 255)');
  });

  test('prints the amounts that are on the screen, and says what they make', async () => {
    await page.goto(`/recipes/${recipeId}`);
    await page.emulateMedia({ media: null });

    await page.getByRole('button', { name: /^(one more|eine mehr)$/i }).click();
    await expect(page.getByRole('region', { name: /ingredients|zutaten/i })).toContainText('300');

    await page.emulateMedia({ media: 'print' });

    // Scaling to three and printing for two would be a quiet lie, so the sheet states what it
    // makes.
    await expect(page.getByRole('region', { name: /ingredients|zutaten/i })).toContainText('300');
    await expect(page.getByRole('region', { name: /steps|zubereitung/i })).toContainText('300');
    await expect(page.getByText(/^3 (servings|Portionen)$/)).toBeVisible();

    await expect(page.getByText(/^2 (servings|Portionen)$/)).toBeHidden();
  });
});
