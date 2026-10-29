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

    // A grid of cards says what the figure means once, on the featured card, not thirty times.
    await expect(card.getByRole('button', { name: 'About calorie values' })).toHaveCount(0);
    const feature = page.locator('.feature').first();
    const info = feature.getByRole('button', { name: 'About calorie values' });
    if (testInfo.project.name === 'desktop') {
      await info.focus();
      await page.keyboard.press('Enter');
    } else {
      await info.tap();
    }
    const explanation = feature.locator('.explanation');
    await expect(explanation).toBeVisible();
    await expect(explanation).toContainText('per serving');
    await expect(page).toHaveURL(/\/$/);
    await expect(page.getByRole('complementary', { name: /^Cooking / })).toBeVisible();
    const accessibility = await new AxeBuilder({ page }).analyze();
    expect(accessibility.violations).toEqual([]);
    await page.keyboard.press('Escape');
    await expect(explanation).toBeHidden();
    await expectReflow(page);
    await page.screenshot({ path: testInfo.outputPath('calorie-overview.png'), fullPage: true });
  });

  for (const [kind, calories] of [
    ['estimated', { atLeast: false, estimated: true }],
    ['lower bound', { atLeast: true, estimated: false }],
    ['estimated lower bound', { atLeast: true, estimated: true }]
  ] as const) {
    test(`the ${kind} figure on the dark featured card is readable in light mode`, async ({
      page
    }) => {
      await page.addInitScript(() =>
        localStorage.setItem(
          'culina.appearance',
          JSON.stringify({ theme: 'warm-paper', mode: 'light' })
        )
      );
      await responsiveData(page, 'en', { nutritionLines: true, suggestions: 1, calories });
      await page.goto('/');
      const figure = page.locator('.feature .calories').first();
      await expect(figure).toBeVisible();

      const contrasts = await figure.evaluate((calories) => {
        // Canvas resolves any CSS color (color-mix included) to the pixel that is painted.
        const context = document.createElement('canvas').getContext('2d', {
          willReadFrequently: true
        })!;
        const luminance = (css: string) => {
          context.clearRect(0, 0, 1, 1);
          context.fillStyle = css;
          context.fillRect(0, 0, 1, 1);
          const [r, g, b] = [...context.getImageData(0, 0, 1, 1).data].map((channel) => {
            const value = channel / 255;
            return value <= 0.03928 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4;
          });
          return 0.2126 * r! + 0.7152 * g! + 0.0722 * b!;
        };
        const background = luminance(
          getComputedStyle(calories.closest('.feature')!).backgroundColor
        );
        return [...calories.querySelectorAll('.qualifier')].map((marker) => {
          const text = luminance(getComputedStyle(marker).color);
          const [light, dark] = text > background ? [text, background] : [background, text];
          return { marker: marker.textContent, ratio: (light + 0.05) / (dark + 0.05) };
        });
      });
      expect(contrasts.length).toBeGreaterThan(0);
      for (const { marker, ratio } of contrasts) {
        expect(ratio, `${marker} on the featured card`).toBeGreaterThanOrEqual(4.5);
      }
    });
  }

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
