import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';
import { expectReflow, responsiveData } from './support/responsive';

test.describe('source review @offline', () => {
  test.use({ serviceWorkers: 'block' });

  test('offers the copy-link import path on iPhone', async ({ page }) => {
    await responsiveData(page, 'en', { activeCooking: false });
    await page.addInitScript(() => {
      Object.defineProperty(navigator, 'userAgent', {
        get: () => 'Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X)'
      });
      Object.defineProperty(navigator, 'clipboard', {
        value: { readText: async () => 'https://www.instagram.com/reel/recipe/' }
      });
    });
    await page.route('**/api/v1/recipe-imports', (route) =>
      route.fulfill({
        json: {
          sourceUrl: 'https://www.instagram.com/reel/recipe/',
          title: 'Beans',
          ingredientLines: ['120 g beans'],
          steps: ['Fry the beans.']
        }
      })
    );
    await page.goto('/recipes/new?text=Beans');
    await expect(
      page.getByText('Culina cannot appear in the iPhone share menu.', { exact: false })
    ).toBeVisible();
    await page.getByRole('button', { name: 'Paste copied link', exact: true }).click();
    await expect(page.getByRole('textbox', { name: 'A link to a recipe' })).toHaveValue(
      'https://www.instagram.com/reel/recipe/'
    );
    await expect(page.getByText('Fry the beans.', { exact: true })).toBeVisible();
  });

  test('keeps a shared caption with its URL, previews before saving, and reflows', async ({
    page
  }, testInfo) => {
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await responsiveData(page, 'en', { activeCooking: false });
    await page.route('**/api/v1/recipe-imports', (route) =>
      route.fulfill({
        json: {
          sourceUrl: 'https://example.com/beans',
          title: 'Beans',
          ingredientLines: ['120 g beans', 'salt'],
          steps: ['Fry the beans.'],
          servings: null,
          totalMinutes: null,
          text: null
        }
      })
    );
    const writes: string[] = [];
    page.on('request', (request) => {
      if (request.method() === 'POST' && new URL(request.url()).pathname === '/api/v1/recipes')
        writes.push(request.url());
    });
    await page.goto(
      '/recipes/new?url=https%3A%2F%2Fexample.com%2Fbeans&text=120%20g%20beans%20%23ad'
    );
    await expect(page.getByRole('textbox', { name: 'The recipe, as text' })).toHaveValue(
      '120 g beans #ad'
    );
    await expect(page.getByRole('button', { name: 'Create recipe', exact: true })).toHaveCount(0);
    // A shared link is filled in, never fetched on arrival: one tap reads it.
    await page.getByRole('button', { name: 'Import recipe', exact: true }).click();
    // The preview on the page is the review: what was understood is shown before anything is written.
    const preview = page.locator('section.paste');
    await expect(preview.getByText('Fry the beans.', { exact: true })).toBeVisible();
    await expect(page.getByRole('dialog')).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Save and edit recipe' })).toBeEnabled();
    expect(writes).toHaveLength(0);
    await expectReflow(page);
    const audit = await new AxeBuilder({ page }).include('section.paste').analyze();
    expect(audit.violations).toEqual([]);
    await page.screenshot({ path: testInfo.outputPath('source-review.png'), fullPage: true });
  });
});

test.describe('saving a reviewed import @offline', () => {
  test.use({ serviceWorkers: 'block', reducedMotion: 'reduce' });
  test('keeps the review on failure and retries the existing recipe', async ({ page }) => {
    await responsiveData(page, 'en', { activeCooking: false });
    const detail = {
      recipeId: '00000000-0000-4000-8000-000000000001',
      householdId: '00000000-0000-4000-8000-000000000002',
      title: 'Beans',
      language: 'en',
      yieldAmount: 1,
      yieldKind: 'servings',
      groups: [],
      steps: [],
      tags: [],
      createdBy: '00000000-0000-4000-8000-000000000003',
      createdAt: '2026-10-05T00:00:00Z',
      updatedAt: '2026-10-05T00:00:00Z',
      version: 1
    };
    let creates = 0;
    let updates = 0;
    await page.route('**/api/v1/recipes', async (route) => {
      if (route.request().method() !== 'POST') return route.fallback();
      creates++;
      await route.fulfill({ status: 201, json: detail });
    });
    await page.route(`**/api/v1/recipes/${detail.recipeId}`, async (route) => {
      if (route.request().method() !== 'PUT') return route.fallback();
      updates++;
      await route.fulfill(
        updates === 1
          ? {
              status: 503,
              json: { code: 'server.unavailable', detail: 'Try again.' }
            }
          : { json: { ...detail, version: 2 } }
      );
    });
    await page.goto(
      '/recipes/new?text=Beans%0AIngredients%0A120%20g%20beans%0AMethod%0AFry%20the%20beans.'
    );
    const preview = page.locator('section.paste');
    const save = preview.getByRole('button', { name: 'Save and edit recipe' });
    await save.click();
    await expect(preview.getByRole('alert')).toBeVisible();
    await expect(preview.getByText('Fry the beans', { exact: true })).toBeVisible();
    await save.click();
    await expect(page).toHaveURL(/\/recipes\/[0-9a-f-]+\/edit/);
    expect(creates).toBe(1);
    expect(updates).toBe(2);
  });
});
