import { expect, test } from '@playwright/test';
import { expectReflow, recipeId, responsiveData } from './support/responsive';

test.describe('hands-free cooking reading @offline', () => {
  test.use({ serviceWorkers: 'block' });

  for (const width of [320, 390]) {
    test(`reads every line of a long step and returns at ${width}px`, async ({
      page
    }, testInfo) => {
      test.setTimeout(60000);
      await page.setViewportSize({ width, height: width === 320 ? 568 : 844 });
      await responsiveData(page, 'de', { longSteps: true });
      await page.emulateMedia({ reducedMotion: 'reduce' });
      await page.goto(`/recipes/${recipeId}/cook`);
      const start = page.getByRole('button', { name: 'Auto-Scroll', exact: true });
      await expect(start).toBeEnabled();
      await page.clock.install();
      await start.click();
      const stop = page.getByRole('button', { name: 'Scrollen stoppen', exact: true });
      await expect(stop).toBeInViewport({ ratio: 1 });
      await expectReflow(page);
      await page.clock.runFor(1200);
      // Nothing is pinned above the step on a phone (the shell header is hidden while cooking), so it rests at the page's own top inset, --space-4.
      expect(
        await page.locator('.step.current').evaluate((el) => el.getBoundingClientRect().top)
      ).toBeGreaterThanOrEqual(16);
      await page.screenshot({ path: testInfo.outputPath('auto-scroll.png') });
      const initial = await page.evaluate(() => scrollY);
      const lastLine = page.locator('.step.current p').last();
      // Reduced motion uses still portions with overlap. Walk through enough
      // portions for the longest narrow-screen fixture to reach its last line.
      for (let i = 0; i < 35; i++) {
        const clear = await lastLine.evaluate((el) => {
          const bottom = document.querySelector('.controls')!.getBoundingClientRect().top;
          return el.getBoundingClientRect().bottom <= bottom;
        });
        if (clear) break;
        await page.clock.runFor(8100);
      }
      const atEnd = await page.evaluate(() => scrollY);
      expect(atEnd).toBeGreaterThan(initial);
      expect(
        await lastLine.evaluate(
          (el) =>
            el.getBoundingClientRect().bottom <=
            document.querySelector('.controls')!.getBoundingClientRect().top
        )
      ).toBe(true);
      await page.clock.runFor(16200);
      expect(await page.evaluate(() => scrollY)).toBeLessThan(atEnd);

      await page.getByRole('button', { name: 'Nächster Schritt', exact: true }).click();
      await page.clock.runFor(1200);
      await expect(page.getByText('Schritt 2 von 4', { exact: true })).toBeVisible();
      const step = page.locator('.step.current');
      expect(await step.evaluate((el) => el.getBoundingClientRect().top)).toBeGreaterThanOrEqual(
        16
      );
      await stop.focus();
      await page.keyboard.press('Space');
      const stopped = await page.evaluate(() => scrollY);
      await page.clock.runFor(20000);
      expect(await page.evaluate(() => scrollY)).toBe(stopped);
      await expect(start).toBeVisible();
    });
  }

  test('continuous reading pauses for notes and stops for a manual scroll', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await responsiveData(page, 'en', { longSteps: true });
    await page.emulateMedia({ reducedMotion: 'no-preference' });
    await page.goto(`/recipes/${recipeId}/cook`);
    const start = page.getByRole('button', { name: 'Auto-scroll', exact: true });
    await expect(start).toBeEnabled();
    await page.clock.install();
    await start.click();
    await page.clock.runFor(1200);
    const initial = await page.evaluate(() => scrollY);
    await page.clock.runFor(7000);
    expect(await page.evaluate(() => scrollY)).toBeGreaterThan(initial + 20);
    await page.getByRole('button', { name: 'Your notes', exact: true }).click();
    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();
    const before = await page.evaluate(() => scrollY);
    await page.clock.runFor(20000);
    expect(await page.evaluate(() => scrollY)).toBe(before);
    await dialog.getByRole('button', { name: 'Close', exact: true }).click();
    await page.clock.runFor(2000);
    expect(await page.evaluate(() => scrollY)).toBeGreaterThan(before);
    await page.evaluate(() => window.dispatchEvent(new WheelEvent('wheel', { deltaY: 100 })));
    await expect(start).toBeVisible();
    const stopped = await page.evaluate(() => scrollY);
    await page.clock.runFor(20000);
    expect(await page.evaluate(() => scrollY)).toBe(stopped);
  });

  test('a fitting step stays still when auto-scroll is enabled', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
    await responsiveData(page, 'en');
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await page.goto(`/recipes/${recipeId}/cook`);
    const start = page.getByRole('button', { name: 'Auto-scroll', exact: true });
    await expect(start).toBeEnabled();
    await page.clock.install();
    await start.click();
    await page.clock.runFor(1200);
    const before = await page.evaluate(() => scrollY);
    await page.clock.runFor(20000);
    expect(await page.evaluate(() => scrollY)).toBe(before);
  });
});
