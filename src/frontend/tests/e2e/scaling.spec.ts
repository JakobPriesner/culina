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
 * The bug this architecture exists to prevent.
 *
 * A step stores a reference to an ingredient rather than the words "200 g
 * butter", so that scaling a recipe cannot leave the ingredient list saying one
 * thing and the instructions another. That is the single most common defect in
 * recipe apps, and it is invisible until somebody is standing at a hob.
 */
// Signed in once for the whole file: signing in is rate limited per account,
// as it should be, and a suite that signs in for every test locks itself out.
test.describe.configure({ mode: 'serial' });

test.describe('scaling a recipe', () => {
  test.skip(needsBackend, skipReason);

  let page: Page;

  test.beforeAll(async ({ browser }, testInfo) => {
    if (needsBackend) {
      return;
    }

    page = await browser.newPage();

    await signInWithHousehold(page, await accountFor(browser, testInfo));
  });

  test.afterAll(async () => {
    await page?.close();
  });

  test('changes the ingredient list and the amounts inside the steps together', async () => {
    const recipeId = await seedRecipe(page, {
      title: unique('Scaling'),
      yieldAmount: 2,
      ingredients: [{ quantity: 200, unit: 'g', name: 'Butter' }],
      steps: ['Melt {0} in a wide pan.']
    });

    await page.goto(`/recipes/${recipeId}`);

    const ingredients = page.getByRole('region', { name: /ingredients|zutaten/i });
    const steps = page.getByRole('region', { name: /steps|zubereitung/i });

    await expect(ingredients).toContainText('200');
    await expect(steps).toContainText('200');

    // Two servings to four, one tap at a time and looking in between — which
    // is what a person does, and what the screen has to keep up with.
    const oneMore = page.getByRole('button', { name: /^(one more|eine mehr)$/i });

    await oneMore.click();
    await expect(ingredients).toContainText('300');

    await oneMore.click();
    await expect(ingredients).toContainText('400');
    // The assertion that matters. A list that scales and a step that does not
    // is how somebody puts half the butter in.
    await expect(steps).toContainText('400');
    await expect(steps).not.toContainText('200');
  });

  test('travels in the URL, so a scaled recipe can be sent to somebody', async () => {
    const recipeId = await seedRecipe(page, {
      title: unique('Shared'),
      yieldAmount: 2,
      ingredients: [{ quantity: 200, unit: 'g', name: 'Butter' }],
      steps: ['Melt {0}.']
    });

    // Opened at six, straight from a link.
    await page.goto(`/recipes/${recipeId}?yield=6`);

    await expect(page.getByRole('region', { name: /ingredients|zutaten/i })).toContainText('600');
    await expect(page.getByRole('region', { name: /steps|zubereitung/i })).toContainText('600');
  });

  test('says so when the amounts stop being trustworthy', async () => {
    const recipeId = await seedRecipe(page, {
      title: unique('Doubtful'),
      yieldAmount: 2,
      ingredients: [{ quantity: 200, unit: 'g', name: 'Butter' }]
    });

    // Far beyond what the times and the tin can survive. A doubled cake in the
    // same tin is a raw cake, and no formula fixes that — so the app says it
    // rather than quietly lying.
    await page.goto(`/recipes/${recipeId}?yield=12`);

    await expect(page.getByText(/time|zeit/i).first()).toBeVisible();
  });

  test('reshapes the recipe around an amount, exactly', async () => {
    const flour = unique('Mehl');
    const recipeId = await seedRecipe(page, {
      title: unique('Anchored'),
      yieldAmount: 4,
      ingredients: [{ quantity: 200, unit: 'g', name: flour }]
    });

    await page.goto(`/recipes/${recipeId}`);
    await page.getByRole('button', { name: /scale to what i have|auf meine menge/i }).click();

    await page.getByLabel(/^\s*(amount|menge)\s*$/i).fill('370 g');
    await page.getByRole('button', { name: /scale the recipe|rezept anpassen/i }).click();

    // 370, not 380. The amount somebody said they had is the whole point of
    // this control, and a tidier number of servings is not worth losing it.
    await expect(page.getByRole('region', { name: /ingredients|zutaten/i })).toContainText(
      /370\s*g/
    );
  });
});
