import { expect, test, type Locator } from '@playwright/test';

import { expectReflow, recipeId, responsiveData } from './support/responsive';

async function expectIndicatorFits(control: Locator, selector: string, underline = false) {
  await expect
    .poll(() =>
      control.evaluate(
        (host, options) => {
          const target = host.querySelector(options.selector)!;
          const indicator = host.querySelector('.selection-indicator')!;
          const a = target.getBoundingClientRect();
          const b = indicator.getBoundingClientRect();
          return (
            Math.abs(a.left - b.left) < 1 &&
            Math.abs(a.width - b.width) < 1 &&
            (options.underline
              ? Math.abs(a.bottom - b.bottom) < 1
              : Math.abs(a.top - b.top) < 1 && Math.abs(a.height - b.height) < 1)
          );
        },
        { selector, underline }
      )
    )
    .toBe(true);
}

test.describe('app flow polish @offline', () => {
  test.use({ serviceWorkers: 'block' });

  test('keeps page geometry stable through modal opening, closing and shorter content', async ({
    page,
    browserName
  }, testInfo) => {
    await page.goto('/design');
    const trigger = page.getByRole('button', { name: 'Open modal', exact: true });
    await trigger.scrollIntoViewIfNeeded();
    const measure = () =>
      page.evaluate(() => ({
        width: document.body.getBoundingClientRect().width,
        x: document.querySelector('h1')!.getBoundingClientRect().left,
        y: scrollY
      }));
    const before = await measure();
    // Use the browser's native scrollbar mode: classic on CI platforms and
    // overlays on macOS. The legacy measured-gap path is covered by unit tests.
    expect(
      await page.evaluate(() => getComputedStyle(document.documentElement).scrollbarGutter)
    ).toBe('stable');
    await trigger.focus();
    await trigger.press('Enter');
    await expect(page.getByRole('dialog')).toBeVisible();
    const opened = await measure();
    expect(opened.width).toBe(before.width);
    expect(opened.x).toBe(before.x);
    expect(opened.y).toBe(before.y);
    if (!(browserName === 'webkit' && testInfo.project.use.isMobile)) {
      await page.mouse.move(5, 5);
      await page.mouse.wheel(0, 400);
    }
    await expect.poll(async () => (await measure()).y).toBe(opened.y);
    await page.keyboard.press('Escape');
    await expect(page.getByRole('dialog')).toBeHidden();
    expect((await measure()).width).toBe(before.width);
    expect((await measure()).x).toBe(before.x);
    await expect(trigger).toBeFocused();
    // The same column remains in place after content no longer needs scrolling.
    await page.addStyleTag({ content: 'body > div { max-height: 100px; overflow: hidden; }' });
    expect((await measure()).width).toBe(before.width);
    expect((await measure()).x).toBe(before.x);
  });

  test('tab marker follows keyboard selection, interruption and resizing', async ({ page }) => {
    await page.goto('/design');
    const strip = page.getByRole('tablist', { name: 'Recipe sections' });
    await page.getByRole('tab', { name: 'Ingredients', exact: true }).focus();
    await page.keyboard.press('ArrowRight');
    await page.keyboard.press('End');
    await expect(page.getByRole('tab', { name: 'Notes', exact: true })).toBeFocused();
    await expect(page.getByRole('tabpanel')).toHaveText('notes');
    await expectIndicatorFits(strip, '[aria-selected=true]', true);
    await page.setViewportSize({ width: 320, height: 720 });
    await expectIndicatorFits(strip, '[aria-selected=true]', true);
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await page.getByRole('tab', { name: 'Steps', exact: true }).click();
    await expectIndicatorFits(strip, '[aria-selected=true]', true);
    expect(
      await strip.locator('.selection-indicator').evaluate((node) => node.getAnimations().length)
    ).toBe(0);
    expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(
      320
    );
  });

  test('peer navigation responds during transitions and aligns its marker at both layouts', async ({
    page
  }, testInfo) => {
    await responsiveData(page, 'en', { activeCooking: false });
    await page.goto('/');
    const nav = page.getByRole('navigation', { name: 'Sections' });
    const visibleNav = nav.filter({ visible: true });
    await page.getByRole('link', { name: 'Cookbooks', exact: true }).click();
    await page.getByRole('link', { name: 'Shopping', exact: true }).click();
    await expect(page).toHaveURL('/shopping');
    const target = testInfo.project.use.isMobile
      ? '[aria-current=page] .icon'
      : '[aria-current=page]';
    await expectIndicatorFits(visibleNav, target);
    await page.getByRole('link', { name: 'Settings', exact: true }).click();
    await expect(page).toHaveURL('/me');
    await expectIndicatorFits(visibleNav, target);
    await expect
      .poll(() =>
        page.evaluate(
          () =>
            document
              .getAnimations()
              .filter(
                (a) =>
                  a.playState === 'running' &&
                  'animationName' in a &&
                  String(a.animationName).startsWith('vt-')
              ).length
        )
      )
      .toBe(0);
    await page.screenshot({ path: testInfo.outputPath('navigation-polish.png') });
    await page.setViewportSize({
      width: testInfo.project.use.isMobile ? 1280 : 390,
      height: 850
    });
    await expectIndicatorFits(
      visibleNav,
      testInfo.project.use.isMobile ? '[aria-current=page]' : '[aria-current=page] .icon'
    );
    await expectReflow(page);
  });

  test('settings categories keep their rail steady and browser Back restores the page', async ({
    page
  }, testInfo) => {
    await responsiveData(page, 'en', { activeCooking: false });
    await page.goto('/me/appearance');
    const rail = page.getByRole('navigation', { name: 'Settings', exact: true });
    const before = await rail.boundingBox();
    await rail.getByRole('link', { name: 'Account', exact: true }).click();
    await expect(page.getByRole('heading', { level: 1, name: 'Account' })).toBeVisible();
    const after = await rail.boundingBox();
    expect(after!.x).toBe(before!.x);
    expect(after!.width).toBe(before!.width);
    expect(await rail.evaluate((node) => getComputedStyle(node).viewTransitionName)).toBe(
      'settings-navigation'
    );
    await page.goBack();
    await expect(page.getByRole('heading', { level: 1, name: 'Appearance' })).toBeVisible();
    await expect
      .poll(() =>
        page.evaluate(
          () =>
            document
              .getAnimations()
              .filter(
                (a) =>
                  a.playState === 'running' &&
                  'animationName' in a &&
                  String(a.animationName).startsWith('vt-')
              ).length
        )
      )
      .toBe(0);
    await page.screenshot({ path: testInfo.outputPath('settings-polish.png') });
    await expectReflow(page);
  });

  test('recipe view control retains readable content and aligns its moving selection', async ({
    page
  }) => {
    await responsiveData(page, 'en', { activeCooking: false });
    await page.goto(`/recipes/${recipeId}`);
    const control = page.locator('.segments');
    await expect(control).toBeVisible();
    const buttons = control.getByRole('button');
    await buttons.nth(1).click();
    await expectIndicatorFits(control, '[aria-pressed=true]');
    await buttons.nth(0).click();
    await expectIndicatorFits(control, '[aria-pressed=true]');
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
    await expectReflow(page);
  });
});
