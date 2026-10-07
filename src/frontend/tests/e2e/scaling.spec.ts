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

// Signed in once for the whole file: sign-in is rate limited per account.
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

    const oneMore = page.getByRole('button', { name: /^(one more|eine mehr)$/i });

    await oneMore.click();
    await expect(ingredients).toContainText('300');

    await oneMore.click();
    await expect(ingredients).toContainText('400');
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

    // Far beyond what the times and the tin can survive: the app says so rather than quietly lying.
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

    await expect(page.getByRole('region', { name: /ingredients|zutaten/i })).toContainText(
      /370\s*g/
    );
  });
});

/** Its own account: the unit choice is a stored preference that would leak into the other tests. */
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

    // Waited for: the choice is pushed to the server, and the next navigation reads it back.
    const saved = () =>
      page.waitForResponse(
        (one) => one.url().includes('/users/me/settings') && one.request().method() === 'PUT'
      );

    await page.goto('/me/appearance');
    await Promise.all([saved(), page.getByLabel(/^(amounts|mengen)$/i).selectOption('imperial')]);

    const settingsRead = page.waitForResponse(
      (one) => one.url().includes('/users/me/settings') && one.request().method() === 'GET'
    );

    await page.goto(`/recipes/${recipeId}`);
    await settingsRead;

    const ingredients = page.getByRole('region', { name: /ingredients|zutaten/i });

    await expect(ingredients).toContainText(/8\s*oz/);
    await expect(ingredients).toContainText(/1\s*cup/i);
    await expect(ingredients).toContainText(/2\s*(tbsp|EL)/);
    await expect(ingredients).toContainText(/3\s*(cloves|zehen)/i);

    // Never cups for a mass: a cup of flour is 120 to 150 g, so "in cups" is not actionable.
    await expect(ingredients).not.toContainText(/cup\s*butter/i);

    await expect(page.getByRole('region', { name: /steps|zubereitung/i })).toContainText(/8\s*oz/);

    await page.goto('/me/appearance');
    await Promise.all([saved(), page.getByLabel(/^(amounts|mengen)$/i).selectOption('metric')]);
    await page.goto(`/recipes/${recipeId}`);

    await expect(ingredients).toContainText(/227\s*g/);
  });
});
