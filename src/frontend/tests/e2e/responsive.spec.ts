import { expect, test, type Page } from '@playwright/test';
import { cookbookId, expectReflow, recipeId, responsiveData } from './support/responsive';

// Boundary pairs catch layouts that fit a phone and a laptop but break in between.
const widths = [320, 390, 639, 640, 767, 768, 1023, 1024, 1279, 1280, 1536];

test.describe('responsive production layouts @offline', () => {
  test.use({ serviceWorkers: 'block' });

  for (const locale of ['de', 'en'] as const) {
    for (const width of widths) {
      test(`${locale} pages reflow at ${width}px`, async ({ page }, testInfo) => {
        // Widths are explicit, so running the matrix twice adds no coverage.
        test.skip(testInfo.project.name !== 'desktop', 'Explicit viewport matrix.');
        await page.setViewportSize({ width, height: 900 });
        await responsiveData(page, locale);
        await page.emulateMedia({ reducedMotion: 'reduce' });
        for (const path of [
          '/login',
          '/register',
          '/',
          '/cookbooks',
          `/cookbooks/${cookbookId}`,
          '/shopping',
          '/plan',
          '/me',
          '/me/appearance',
          '/me/household',
          '/recipes/new',
          `/recipes/${recipeId}`,
          `/recipes/${recipeId}/edit`,
          `/recipes/${recipeId}/cook`
        ]) {
          await test.step(path, async () => {
            await page.goto(path);
            await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
            await expect(page.locator('[aria-busy="true"]')).toHaveCount(0);
            await expectReflow(page);
            if (path === '/login' || path === '/register') return;
            await expect(page.locator('nav.nav:visible')).toHaveCount(1);
            await expect(page.locator(width < 1024 ? 'nav.bottom' : 'nav.top')).toBeVisible();
            if (
              locale === 'de' &&
              [320, 768, 1280].includes(width) &&
              ['/', '/shopping', '/plan', `/recipes/${recipeId}`].includes(path)
            ) {
              await page.screenshot({
                path: testInfo.outputPath(`${width}-${path.replaceAll('/', '_') || 'home'}.png`)
              });
            }
            if (path === '/shopping' || path.endsWith('/edit')) {
              // Text remains useful to type into, even when the outer row fits.
              const name = page.locator(
                path === '/shopping' ? '#shopping-add-name' : '#add-ingredient-name'
              );
              await expect(name).toBeVisible();
              expect((await name.boundingBox())!.width).toBeGreaterThanOrEqual(150);
            }
            if (path === '/' || path === '/plan') {
              const grid = page.locator(path === '/' ? 'ul.grid' : 'ol.week');
              const columns = await grid.evaluate(
                (el) => getComputedStyle(el).gridTemplateColumns.split(' ').length
              );
              if (path === '/plan') {
                for (const name of await page.locator('.card .name').all()) {
                  expect((await name.boundingBox())!.width).toBeGreaterThanOrEqual(100);
                }
              }
              expect(columns).toBe(
                width < 640 ? 1 : width < 1024 ? 2 : path === '/plan' && width >= 1280 ? 7 : 3
              );
            }
          });
        }
      });
    }
  }

  test('narrow editor, planner picker and cooking controls remain usable', async ({ page }) => {
    await page.setViewportSize({ width: 320, height: 720 });
    await responsiveData(page);
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await page.goto(`/recipes/${recipeId}/edit`);
    await page
      .getByRole('button', {
        name: /Sonnenblumenkernvollkornbrot bearbeiten/i
      })
      .click();
    await expectReflow(page);
    await expect(page.locator('#ingredient-0-name')).toBeVisible();
    expect((await page.locator('#ingredient-0-name').boundingBox())!.width).toBeGreaterThan(200);
    await page.goto('/plan');
    await page.getByRole('button', { name: /^Weiter →$/i }).click();
    await expectReflow(page);
    await page
      .getByRole('button', { name: /Hinzufügen/i })
      .first()
      .click();
    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();
    await expectReflow(page);
    await page.keyboard.press('Escape');
    await page.goto(`/recipes/${recipeId}/cook`);
    const next = page.getByRole('button', { name: /nächster schritt/i });
    await expect(next).toBeEnabled();
    await next.scrollIntoViewIfNeeded();
    await expect(next).toBeInViewport({ ratio: 1 });
    expect((await next.boundingBox())!.height).toBeGreaterThanOrEqual(56);
    const nav = (await page.locator('nav.bottom').boundingBox())!;
    const button = (await next.boundingBox())!;
    expect(button.y + button.height).toBeLessThanOrEqual(nav.y);
    const stop = page.locator('article.cooking > footer');
    await stop.scrollIntoViewIfNeeded();
    const stopBox = (await stop.boundingBox())!;
    const controls = (await page.locator('.controls:has(.moves)').boundingBox())!;
    expect(stopBox.y + stopBox.height).toBeLessThanOrEqual(controls.y);
    await next.click();
    await expect(page.getByText('Schritt 2 von 4', { exact: true })).toBeVisible();
    await expectReflow(page);
  });

  for (const [width, height] of [
    [320, 568],
    [375, 812],
    [390, 844],
    [430, 932]
  ] as const) {
    test(`the step being cooked is readable above the controls at ${width}px`, async ({
      page
    }, testInfo) => {
      test.skip(testInfo.project.name !== 'desktop', 'Explicit viewport matrix.');
      await page.setViewportSize({ width, height });
      await responsiveData(page);
      await page.emulateMedia({ reducedMotion: 'reduce' });
      await page.goto(`/recipes/${recipeId}/cook`);
      const next = page.getByRole('button', { name: /nächster schritt/i });
      await expect(next).toBeEnabled();

      // Arriving, every step after it, and the phone turned on its side.
      await expectCurrentStepReadable(page);
      for (let step = 2; step <= 4; step++) {
        await next.click();
        await expect(page.getByText(`Schritt ${step} von 4`, { exact: true })).toBeVisible();
        await expectCurrentStepReadable(page);
      }
      await page.setViewportSize({ width: height, height: width });
      await expectCurrentStepReadable(page);
      await page.screenshot({ path: testInfo.outputPath(`cook-${width}.png`) });
    });
  }

  test('short landscape keeps forms, dialogs and cooking reachable', async ({ page }) => {
    await page.setViewportSize({ width: 844, height: 390 });
    await responsiveData(page);
    await page.emulateMedia({ reducedMotion: 'reduce' });
    for (const path of ['/login', '/register', '/shopping', `/recipes/${recipeId}/cook`]) {
      await page.goto(path);
      await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
      await expectReflow(page);
    }
    const next = page.getByRole('button', { name: /nächster schritt/i });
    await next.scrollIntoViewIfNeeded();
    await expect(next).toBeInViewport({ ratio: 1 });
    await page.goto('/plan');
    await page
      .getByRole('button', { name: /Hinzufügen/i })
      .first()
      .click();
    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();
    const box = (await dialog.boundingBox())!;
    expect(box.y).toBeGreaterThanOrEqual(0);
    expect(box.y + box.height).toBeLessThanOrEqual(390);
    await expect(dialog.getByRole('button', { name: /schließen/i })).toBeInViewport({ ratio: 1 });
    await expectReflow(page);
  });
});

/**
 * The current step starts clear of the header and, scrolled by no more than
 * its own overhang, ends clear of the controls and the bottom navigation.
 * Polled, because the page follows a move once the steps stop resizing.
 *
 * Measured only after two frames. A resize is answered on the next frame, not
 * when `setViewportSize` returns, and the page then brings the step back into
 * the clear. Scrolling before that is scrolling against the page: on a slow
 * runner the rescue lands after the test's own scroll and puts the step back
 * where the rescue wants it, overhang and all.
 */
async function expectCurrentStepReadable(page: Page) {
  await page.evaluate(
    () => new Promise((done) => requestAnimationFrame(() => requestAnimationFrame(done)))
  );

  const measure = () =>
    page.evaluate(() => {
      const top = (selector: string) =>
        document.querySelector(selector)?.getBoundingClientRect().top ?? innerHeight;
      const step = document.querySelector('.step.current')!.getBoundingClientRect();
      return {
        top: step.top,
        bottom: step.bottom,
        // A header scrolled off the top clears nothing below the screen's edge.
        clearTop: Math.max(
          0,
          document.querySelector('header.header')!.getBoundingClientRect().bottom
        ),
        clearBottom: Math.min(top('.controls:has(.moves)'), top('nav.bottom'), innerHeight)
      };
    });

  await expect
    .poll(async () => {
      const at = await measure();
      return at.top >= at.clearTop && at.top < at.clearBottom;
    })
    .toBe(true);

  const at = await measure();
  await page.evaluate((by) => scrollBy(0, by), Math.max(0, at.bottom - at.clearBottom));
  const scrolled = await measure();
  expect(scrolled.bottom).toBeLessThanOrEqual(scrolled.clearBottom + 1);
}
