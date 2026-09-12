import { expect, test } from '@playwright/test';

test.describe('recipe experience preview @offline', () => {
  test.beforeEach(async ({ page }) => {
    await page.addInitScript(() => localStorage.setItem('culina.locale', 'en'));
    await page.goto('/design/recipes');
    await expect(page.getByRole('heading', { name: 'Your recipe book.' })).toBeVisible();
  });

  test('filters, finds ingredients and recovers from empty results', async ({ page }) => {
    await page.getByRole('button', { name: 'Up to 30 min', exact: true }).click();
    await expect(page.getByRole('status')).toHaveText('2 recipes');
    const search = page.getByRole('searchbox');
    await search.fill('tomatoes');
    await expect(page.getByRole('status')).toHaveText('1 recipe');
    await search.fill('unfindable');
    await expect(page.getByRole('heading', { name: 'Nothing here just yet.' })).toBeVisible();
    await page.getByRole('button', { name: 'Show all recipes', exact: true }).click();
    await expect(page.getByRole('status')).toHaveText('3 recipes');
  });

  test('keeps favourites independent from opening a recipe', async ({ page }) => {
    await page
      .getByRole('button', { name: 'Favourite: Tomatoes on sourdough', exact: true })
      .click();
    await page.getByRole('button', { name: 'Favourites', exact: true }).click();
    await expect(page.getByRole('status')).toHaveText('2 recipes');
    await page.getByRole('button', { name: 'Tomatoes on sourdough', exact: true }).click();
    await expect(page.getByRole('heading', { level: 1 })).toHaveText('Tomatoes on sourdough');
  });

  test('scales, checks ingredients and keeps their state through cooking', async ({ page }) => {
    await page.getByRole('button', { name: 'View recipe', exact: true }).click();
    await page.getByRole('button', { name: 'One more serving', exact: true }).click();
    await expect(page.getByRole('spinbutton', { name: 'Servings' })).toHaveValue('3');
    await expect(page.getByText('300 g', { exact: true })).toBeVisible();
    await page.getByRole('checkbox', { name: 'Orzo', exact: true }).check();
    await page.getByRole('button', { name: 'Start cooking', exact: true }).click();
    await expect(page.getByText('Step 1 of 3', { exact: true })).toBeVisible();
    await page.getByRole('button', { name: 'Next step', exact: true }).click();
    await expect(page.getByText('Step 2 of 3', { exact: true })).toBeVisible();
    await page.getByRole('button', { name: 'Back to reading', exact: true }).click();
    await expect(page.getByRole('checkbox', { name: 'Orzo', exact: true })).toBeChecked();
    await expect(page.getByRole('spinbutton', { name: 'Servings' })).toHaveValue('3');
  });
});
