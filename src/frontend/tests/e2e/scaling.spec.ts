import { expect, test, type Page } from '@playwright/test';

import {
  accountFor,
  ensureAccount,
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

/**
 * The same recipe, in the units the reader owns.
 *
 * Its own account, and not by accident: the choice is a stored preference, and
 * a test that changed it on the account the rest of this file shares would
 * leave every other test reading ounces.
 */
test.describe('measured the way the reader measures', () => {
  test.skip(needsBackend, skipReason);

  let page: Page;

  test.beforeAll(async ({ browser }) => {
    if (needsBackend) {
      return;
    }

    page = await browser.newPage();

    await signInWithHousehold(page, await ensureAccount(browser, 'imperial'));
  });

  test.afterAll(async () => {
    await page?.close();
  });

  test('converts mass and volume, and leaves everything else alone', async () => {
    const recipeId = await seedRecipe(page, {
      title: unique('Imperial'),
      yieldAmount: 2,
      ingredients: [
        { quantity: 227, unit: 'g', name: 'Butter' },
        { quantity: 240, unit: 'ml', name: 'Milk' },
        { quantity: 2, unit: 'tbsp', name: 'Oil' },
        { quantity: 3, unit: 'clove', name: 'Garlic' }
      ],
      steps: ['Melt {0}.']
    });

    // Waited for, not assumed: the choice is pushed to the server, and the next
    // navigation reboots the app and reads it back from there.
    const saved = () =>
      page.waitForResponse(
        (one) => one.url().includes('/users/me/settings') && one.request().method() === 'PUT'
      );

    await page.goto('/me');
    await Promise.all([saved(), page.getByLabel(/^(amounts|mengen)$/i).selectOption('imperial')]);

    // The app reads the choice back on boot, and the amounts are rendered from
    // it. Asserting before that read has landed is asserting on the default.
    const settingsRead = page.waitForResponse(
      (one) => one.url().includes('/users/me/settings') && one.request().method() === 'GET'
    );

    await page.goto(`/recipes/${recipeId}`);
    await settingsRead;

    const ingredients = page.getByRole('region', { name: /ingredients|zutaten/i });

    await expect(ingredients).toContainText(/8\s*oz/);
    await expect(ingredients).toContainText(/1\s*cup/);
    // A spoon is a spoon in both systems, and a clove is a clove.
    await expect(ingredients).toContainText(/2\s*(tbsp|EL)/);
    await expect(ingredients).toContainText(/3\s*cloves/);

    // Never cups for a mass: a cup of flour is between 120 g and 150 g
    // depending on how it was packed, so "in cups" is a number nobody can act
    // on. The butter is ounces.
    await expect(ingredients).not.toContainText('cup Butter');

    // The step carries the converted amount too, because it carries the
    // ingredient rather than a number somebody typed into it.
    await expect(page.getByRole('region', { name: /steps|zubereitung/i })).toContainText(/8\s*oz/);

    // Shown, not stored: switching back leaves the recipe exactly as written.
    await page.goto('/me');
    await Promise.all([saved(), page.getByLabel(/^(amounts|mengen)$/i).selectOption('metric')]);
    await page.goto(`/recipes/${recipeId}`);

    await expect(ingredients).toContainText(/227\s*g/);
  });
});
