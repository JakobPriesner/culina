import { expect, test, type Page } from '@playwright/test';

import {
  accountFor,
  needsBackend,
  seedRecipe,
  signInWithHousehold,
  skipReason,
  unique
} from './support/culina';

test.describe.configure({ mode: 'serial' });

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

    await expect(page.getByRole('region', { name: /your attempts|deine versuche/i })).toBeHidden();

    await page.getByRole('button', { name: /start cooking|kochen starten/i }).click();
    await page.getByRole('button', { name: /^(i made it|hab ich gekocht)$/i }).click();

    // Waited for: navigating right away races the write that creates the attempt.
    await expect(
      page.getByText(/added to your cooking history|zu deiner kochhistorie/i)
    ).toBeVisible();

    await page.goto(`/recipes/${recipeId}`);

    const strip = page.getByRole('region', { name: /your attempts|deine versuche/i });

    await expect(strip).toBeVisible();

    await page.getByRole('button', { name: /add a photo|foto vom/i }).click();
    await page
      .getByLabel(/choose a photo|foto auswählen/i)
      .setInputFiles({ name: 'dinner.png', mimeType: 'image/png', buffer: png });

    await expect(strip.getByRole('img')).toBeVisible();

    await expect(strip.getByRole('img')).toHaveAttribute(
      'src',
      new RegExp(`/recipes/${recipeId}/cook-log/[0-9a-f-]+/photo`)
    );

    await page.getByRole('button', { name: /^(remove|entfernen)$/i }).click();

    await expect(strip.getByRole('img')).toBeHidden();
    await expect(strip).toBeVisible();
  });
});
