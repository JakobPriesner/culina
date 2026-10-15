import { expect, test } from '@playwright/test';
import { expectReflow, responsiveData } from './support/responsive';

test.describe('mobile app bar @offline', () => {
  test.use({ serviceWorkers: 'block' });

  for (const width of [320, 390, 768]) {
    test(`bottom navigation stays usable at ${width}px`, async ({ page }) => {
      await page.setViewportSize({ width, height: 844 });
      await responsiveData(page, 'en', { activeCooking: false });
      await page.goto('/');
      await expect(page.locator('[aria-busy="true"]')).toHaveCount(0);
      await page.screenshot({ path: test.info().outputPath(`appbar-home-${width}.png`) });
      const nav = page.locator('nav.bottom');
      const links = nav.getByRole('link');
      await expect(links).toHaveCount(4);
      const dock = (await nav.boundingBox())!;
      expect(dock.x).toBeGreaterThanOrEqual(12);
      expect(dock.x + dock.width).toBeLessThanOrEqual(width - 12);
      expect(dock.y + dock.height).toBeLessThanOrEqual(844 - 8);
      const boxes = await Promise.all((await links.all()).map((link) => link.boundingBox()));
      for (const box of boxes) {
        expect(box!.width).toBeGreaterThanOrEqual(44);
        expect(box!.height).toBeGreaterThanOrEqual(44);
      }
      expect(
        Math.max(...boxes.map((box) => box!.width)) - Math.min(...boxes.map((box) => box!.width))
      ).toBeLessThan(1);
      await expect(nav.locator('[aria-current="page"]')).toHaveText('Recipes');
      await links.filter({ hasText: 'Cookbooks' }).click();
      await expect(nav.locator('[aria-current="page"]')).toHaveText('Cookbooks');
      await expectReflow(page);
      await page.screenshot({ path: test.info().outputPath(`appbar-${width}.png`) });
      await page.evaluate(() => document.documentElement.setAttribute('data-mode', 'dark'));
      await page.screenshot({ path: test.info().outputPath(`appbar-dark-${width}.png`) });
      await page.evaluate(() => {
        document.documentElement.style.fontSize = '32px';
      });
      await expectReflow(page);
    });
  }
});
