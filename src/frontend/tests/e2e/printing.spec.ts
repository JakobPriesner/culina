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
 * A recipe on paper.
 *
 * The oldest way to use one, and the thing almost every recipe app gets wrong:
 * four pages, a navigation bar, and the amounts from before you scaled it.
 */
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

    // The recipe is there.
    await expect(page.getByRole('region', { name: /ingredients|zutaten/i })).toContainText(
      'Butter'
    );
    await expect(page.getByRole('region', { name: /steps|zubereitung/i })).toContainText('Melt');

    // The app is not. A navigation bar on paper is four square centimetres of
    // toner that cannot be tapped.
    await expect(page.getByRole('banner')).toBeHidden();
    await expect(page.getByRole('navigation').first()).toBeHidden();
    await expect(page.getByRole('link', { name: /skip to content|zum inhalt/i })).toBeHidden();

    // And neither is anything that exists to be pressed: a printed button is a
    // small lie about what the paper can do.
    await expect(page.getByRole('button', { name: /start cooking|kochen starten/i })).toBeHidden();

    // Black on white, whatever the reader's theme. A dark app that prints as
    // dark is a page of toner behind every word.
    const ground = await page.evaluate(() => getComputedStyle(document.body).backgroundColor);

    expect(ground).toBe('rgb(255, 255, 255)');
  });

  test('prints the amounts that are on the screen, and says what they make', async () => {
    await page.goto(`/recipes/${recipeId}`);
    await page.emulateMedia({ media: null });

    await page.getByRole('button', { name: /^(one more|eine mehr)$/i }).click();
    await expect(page.getByRole('region', { name: /ingredients|zutaten/i })).toContainText('300');

    await page.emulateMedia({ media: 'print' });

    // The whole point. Scaling a recipe to three and printing it for two is
    // precisely the quiet lie this app is built to avoid — so the sheet says
    // what it makes, because paper has no servings control to explain itself.
    await expect(page.getByRole('region', { name: /ingredients|zutaten/i })).toContainText('300');
    await expect(page.getByRole('region', { name: /steps|zubereitung/i })).toContainText('300');
    await expect(page.getByText(/^3 (servings|Portionen)$/)).toBeVisible();

    // And says it once. The recipe was written for two, and a sheet headed
    // "2 servings" above a list scaled to three is the lie in a second place.
    await expect(page.getByText(/^2 (servings|Portionen)$/)).toBeHidden();
  });
});
