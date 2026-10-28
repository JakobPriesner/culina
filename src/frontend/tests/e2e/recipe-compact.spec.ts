import { expect, test } from '@playwright/test';

import { expectReflow, recipeId, responsiveData } from './support/responsive';

/** The recipe page's cook action and title head, and the editor's status rail, where a phone or tablet has little room. */
test.describe('the recipe page and editor on small screens @offline', () => {
  test.use({ serviceWorkers: 'block' });

  for (const [width, height] of [
    [320, 568],
    [390, 844],
    [844, 390]
  ] as const) {
    test(`the cook action is one line above the navigation at ${width}x${height}`, async ({
      page
    }) => {
      await page.setViewportSize({ width, height });
      await responsiveData(page, 'de', { activeCooking: false });
      await page.goto(`/recipes/${recipeId}`);

      const start = page.getByRole('button', { name: 'Kochen starten' });
      const made = page.getByRole('button', { name: 'Hab ich gekocht' });

      await expect(start).toBeVisible();
      await expect(made).toBeVisible();

      for (const button of [start, made]) {
        const box = (await button.boundingBox())!;

        expect(box.height).toBeLessThan(64);
        expect(box.height).toBeGreaterThanOrEqual(44);
        expect(box.width).toBeGreaterThanOrEqual(44);
      }

      const startBox = (await start.boundingBox())!;
      const nav = page.locator('nav.bottom');

      if (await nav.isVisible()) {
        expect(startBox.y + startBox.height).toBeLessThanOrEqual((await nav.boundingBox())!.y);
      }

      await page.screenshot({ path: test.info().outputPath(`cook-action-${width}.png`) });
      await page.evaluate(() => window.scrollTo(0, document.documentElement.scrollHeight));
      await page.screenshot({ path: test.info().outputPath(`cook-action-end-${width}.png`) });
      await expectReflow(page);
    });
  }

  for (const [width, height] of [
    [768, 1024],
    [1024, 768],
    [1024, 1366],
    [1440, 900]
  ] as const) {
    test(`the title keeps a readable measure at ${width}x${height}`, async ({ page }) => {
      await page.setViewportSize({ width, height });
      await responsiveData(page, 'de', { activeCooking: false });
      await page.goto(`/recipes/${recipeId}`);

      const title = page.getByRole('heading', { level: 1 });

      await expect(title).toBeVisible();

      const box = (await title.boundingBox())!;
      const lineHeight = await title.evaluate((el) => parseFloat(getComputedStyle(el).lineHeight));

      await page.screenshot({ path: test.info().outputPath(`title-${width}x${height}.png`) });
      expect(box.height / lineHeight).toBeLessThanOrEqual(2.5);
    });
  }

  for (const width of [320, 390]) {
    test(`the editor rail names the recipe at ${width}px`, async ({ page }) => {
      await page.setViewportSize({ width, height: 700 });
      await responsiveData(page, 'de', { activeCooking: false });
      await page.goto(`/recipes/${recipeId}/edit`);
      await page.getByRole('heading', { name: 'Zutaten' }).first().scrollIntoViewIfNeeded();

      const title = page.locator('.rail .title');

      await expect(title).toBeVisible();
      expect((await title.boundingBox())!.width).toBeGreaterThan(width * 0.4);
      await expect(page.locator('.rail').getByRole('status')).toHaveAccessibleName(
        /Speichert beim Schreiben|Gespeichert/
      );
      await page.screenshot({ path: test.info().outputPath(`rail-${width}.png`) });
    });
  }
});
