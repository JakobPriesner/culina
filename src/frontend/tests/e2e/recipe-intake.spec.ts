import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';
import { expectReflow, responsiveData } from './support/responsive';

test.describe('source review @offline', () => {
  test.use({ serviceWorkers: 'block' });

  test('keeps a shared caption with its URL, reviews before saving, and reflows', async ({
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
    await page.getByRole('button', { name: 'Review recipe', exact: true }).click();
    const dialog = page.getByRole('dialog', { name: 'Review the recipe' });
    await expect(dialog).toBeVisible();
    await expect(dialog.getByText('120 g beans #ad', { exact: true })).toBeVisible();
    await expect(dialog.getByText('Fry the beans.', { exact: true })).toBeVisible();
    await expect(dialog.getByRole('link', { name: 'Open original' })).toHaveAttribute(
      'href',
      'https://example.com/beans'
    );
    expect(writes).toHaveLength(0);
    await expectReflow(page);
    const audit = await new AxeBuilder({ page }).include('dialog').analyze();
    expect(audit.violations).toEqual([]);
    await page.screenshot({ path: testInfo.outputPath('source-review.png'), fullPage: true });
    await dialog.getByRole('button', { name: 'Discard', exact: true }).click();
    await expect(page.getByRole('textbox', { name: 'The recipe, as text' })).toHaveValue(
      '120 g beans #ad'
    );
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
    await page.getByRole('button', { name: 'Review recipe', exact: true }).click();
    const dialog = page.getByRole('dialog');
    const save = dialog.getByRole('button', { name: 'Save and edit recipe' });
    await save.click();
    await expect(dialog.getByRole('alert')).toBeVisible();
    await expect(dialog.getByText('Fry the beans', { exact: true })).toBeVisible();
    await save.click();
    await expect(page).toHaveURL(/\/recipes\/[0-9a-f-]+\/edit/);
    expect(creates).toBe(1);
    expect(updates).toBe(2);
  });
});
