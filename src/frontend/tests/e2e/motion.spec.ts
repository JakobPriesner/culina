import { expect, test } from '@playwright/test';
import { expectReflow, responsiveData } from './support/responsive';

test.describe('polished motion @offline', () => {
  test.use({ serviceWorkers: 'block' });

  test('closing paints an exit while native focus returns immediately', async ({ page }) => {
    await page.goto('/design');
    const trigger = page.getByRole('button', { name: 'Open modal', exact: true });
    await trigger.click();
    const dialog = page.getByRole('dialog', { name: 'Rename recipe' });
    await expect(dialog).toBeVisible();
    await expect.poll(() => dialog.evaluate((node) => getComputedStyle(node).opacity)).toBe('1');
    // Observe the native close itself, before a Playwright assertion can wait
    // out the exit. State and focus must close immediately; only paint remains.
    const state = await dialog.evaluate((node: HTMLDialogElement) => {
      node.close();
      return {
        open: node.open,
        supported: CSS.supports('overlay', 'auto'),
        painting: getComputedStyle(node).display !== 'none',
        focus: document.activeElement?.textContent?.trim()
      };
    });
    expect(state.open).toBe(false);
    expect(state.focus).toBe('Open modal');
    if (state.supported) expect(state.painting).toBe(true);
    await expect(dialog).toBeHidden();
    await trigger.click();
    await expect(dialog).toBeVisible();
    await page.keyboard.press('Escape');
    await expect(dialog).toBeHidden();
  });

  test('reduced motion opens and closes without travel or a delayed exit', async ({ page }) => {
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await page.goto('/design');
    await page.getByRole('button', { name: 'Open sheet', exact: true }).click();
    const dialog = page.getByRole('dialog');
    expect(await dialog.evaluate((node) => getComputedStyle(node).transform)).toBe('none');
    const display = await dialog.evaluate((node: HTMLDialogElement) => {
      node.close();
      return getComputedStyle(node).display;
    });
    expect(display).toBe('none');
    await expect(page.getByRole('button', { name: 'Open sheet', exact: true })).toBeFocused();
  });

  test('Olli stays legible at 320px with motion disabled and no inline controls', async ({
    page
  }) => {
    await page.setViewportSize({ width: 320, height: 720 });
    await responsiveData(page, 'en', { activeCooking: false });
    await page.goto('/design#ai-motion');
    const work = page.locator('#ai-motion');
    const olli = work.locator('svg.olli');
    const expectWorkFits = async () => {
      // The gallery has unrelated clipped photo effects; bound this assertion
      // to the actual work scene and its interactive/content boxes.
      expect(
        await work.locator('svg.olli, [role=status], button').evaluateAll((nodes) =>
          nodes.every((node) => {
            const box = node.getBoundingClientRect();
            return box.left >= -1 && box.right <= window.innerWidth + 1;
          })
        )
      ).toBe(true);
      expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(
        320
      );
    };
    await expect(olli).toBeVisible();
    expect((await olli.boundingBox())!.width).toBeGreaterThanOrEqual(96);
    await expect(work.getByRole('button')).toHaveCount(0);
    await expect(work.getByRole('status')).toHaveText('Refining your recipe…');
    await expectWorkFits();
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await expect(work.getByRole('button')).toHaveCount(0);
    await expect(olli).toBeVisible();
    await page.addInitScript(() => localStorage.setItem('culina.olli', 'hidden'));
    await page.reload();
    await expect(work.locator('svg.olli')).toBeVisible();
    await expect(work.getByRole('status')).toHaveText('Refining your recipe…');
    await expectWorkFits();
  });

  test('consecutive page navigation keeps the content usable', async ({ page }) => {
    await responsiveData(page, 'en', { activeCooking: false });
    await page.goto('/recipes');
    const content = page.locator('#content');
    expect(await content.evaluate((node) => getComputedStyle(node).viewTransitionName)).toBe(
      'page-content'
    );
    await page.getByRole('link', { name: 'Cookbooks', exact: true }).click();
    await expect(page).toHaveURL('/cookbooks');
    await page.getByRole('link', { name: 'Shopping', exact: true }).click();
    await expect(page).toHaveURL('/shopping');
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
    await expectReflow(page);
  });
});
