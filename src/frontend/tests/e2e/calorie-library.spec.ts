import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';

import { expectReflow, responsiveData } from './support/responsive';

test.describe('calories in the overview @offline', () => {
  test.use({ serviceWorkers: 'block' });

  test('shows discreet values on regular and featured cards with touch and keyboard info', async ({
    page
  }, testInfo) => {
    await responsiveData(page, 'en', {
      nutritionLines: true,
      suggestions: 1,
      activeCooking: true
    });
    await page.goto('/');
    const card = page.locator('article.recipe').first();
    await expect(card.locator('.calories')).toContainText('520');
    await expect(card.locator('.calories')).toContainText('kcal');
    await expect(page.locator('.feature .calories').first()).toBeVisible();

    const info = card.getByRole('button', { name: 'About calorie values' });
    if (testInfo.project.name === 'desktop') {
      await info.focus();
      await page.keyboard.press('Enter');
    } else {
      await info.tap();
    }
    await expect(card.locator('.explanation')).toBeVisible();
    await expect(card.locator('.explanation')).toContainText('minimum');
    await expect(card.locator('.explanation')).toContainText('per serving');
    await expect(page).toHaveURL(/\/$/);
    await expect(page.getByRole('complementary', { name: /^Cooking / })).toBeVisible();
    const accessibility = await new AxeBuilder({ page }).analyze();
    expect(accessibility.violations).toEqual([]);
    await page.keyboard.press('Escape');
    await expect(card.locator('.explanation')).toBeHidden();
    await expectReflow(page);
    await page.screenshot({ path: testInfo.outputPath('calorie-overview.png'), fullPage: true });
  });

  test('applies the calorie filter to the request and removes it from its chip', async ({
    page
  }, testInfo) => {
    await responsiveData(page, 'en', { nutritionLines: true, activeCooking: true });
    await page.goto('/');
    await page.getByRole('button', { name: 'Filter', exact: true }).click();
    const panel = page.getByRole('dialog', { name: 'Filter and sort' });
    await expect(panel.getByText('Calories', { exact: true })).toBeVisible();
    const requested = page.waitForRequest((request) => {
      const url = new URL(request.url());
      return url.pathname === '/api/v1/recipes' && url.searchParams.get('maxKcal') === '500';
    });
    await panel.getByRole('button', { name: 'Up to 500 kcal', exact: true }).click();
    await requested;
    await expect(
      panel.getByRole('button', { name: 'Up to 500 kcal', exact: true })
    ).toHaveAttribute('aria-pressed', 'true');
    await panel.screenshot({ path: testInfo.outputPath('calorie-filter.png') });
    await panel.locator('footer').getByRole('button', { name: 'Done', exact: true }).click();
    const unfiltered = page.waitForRequest((request) => {
      const url = new URL(request.url());
      return url.pathname === '/api/v1/recipes' && !url.searchParams.has('maxKcal');
    });
    await page.getByRole('button', { name: 'Remove the filter “Up to 500 kcal”' }).click();
    await unfiltered;
    await expectReflow(page);
  });
});
