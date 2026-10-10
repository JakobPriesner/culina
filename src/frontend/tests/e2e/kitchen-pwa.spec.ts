import { expect, test } from '@playwright/test';
import {
  expectReflow,
  openShoppingFromCooking,
  recipeId,
  responsiveData
} from './support/responsive';

test.describe('kitchen display and badge lifecycle @offline', () => {
  test.use({ serviceWorkers: 'block' });
  test('shows readable kitchen modes and preserves timers across steps and routes', async ({
    page
  }, testInfo) => {
    await responsiveData(page, 'en');
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await page.route('**/shopping-list/items/item-1', (route) =>
      route.fulfill({
        json: {
          listId: 'list-1',
          version: 2,
          items: [
            {
              itemId: 'item-1',
              name: 'Sonnenblumenkernvollkornbrot',
              quantity: 200,
              unit: 'g',
              section: 'bakery',
              isChecked: true,
              isManual: true,
              sources: []
            }
          ]
        }
      })
    );
    await page.addInitScript(() => {
      const state = window as unknown as { badgeWrites: number[] };
      state.badgeWrites = [];
      Object.defineProperty(navigator, 'setAppBadge', {
        configurable: true,
        value: async (count: number) => {
          state.badgeWrites.push(count);
        }
      });
      Object.defineProperty(navigator, 'clearAppBadge', {
        configurable: true,
        value: async () => {
          state.badgeWrites.push(0);
        }
      });
      Object.defineProperty(navigator, 'wakeLock', {
        configurable: true,
        value: {
          request: async () => {
            const lock = new EventTarget() as EventTarget & {
              released: boolean;
              release: () => Promise<void>;
            };
            lock.released = false;
            lock.release = async () => {
              lock.released = true;
              lock.dispatchEvent(new Event('release'));
            };
            return lock;
          }
        }
      });
      localStorage.setItem(
        'culina.timers.session-1',
        JSON.stringify([
          { stepIndex: 0, endsAt: Date.now() + 600_000, label: 'Simmer' },
          { stepIndex: 1, endsAt: Date.now() + 600_000, label: 'Bake' }
        ])
      );
    });
    await page.goto(`/recipes/${recipeId}/cook`);
    const kitchen = page.getByRole('button', { name: 'Kitchen controls' });
    await expect(kitchen).toBeInViewport();
    await kitchen.click();
    const display = page.getByLabel('Kitchen display');
    await expect(display).toBeVisible();
    await expect(page.getByText('Screen stays awake')).toBeVisible();
    await expect
      .poll(() =>
        page.evaluate(() => (window as unknown as { badgeWrites: number[] }).badgeWrites.at(-1))
      )
      .toBe(2);
    for (const mode of ['glare', 'oled']) {
      await display.selectOption(mode);
      await expect(page.locator('html')).toHaveAttribute('data-kitchen-lighting', mode);
      await expectReflow(page);
      const background = await page.evaluate(() => getComputedStyle(document.body).backgroundColor);
      expect(background).toBe(mode === 'glare' ? 'rgb(255, 255, 255)' : 'rgb(0, 0, 0)');
      await page.screenshot({ path: testInfo.outputPath(`kitchen-${mode}.png`), fullPage: true });
    }
    await page
      .getByRole('dialog', { name: 'Kitchen controls' })
      .getByRole('button', { name: 'Close', exact: true })
      .click();
    await page.getByRole('button', { name: 'Next step', exact: true }).click();
    await expect(kitchen).toBeInViewport();
    await expect(page.getByText('Step 2 of 4', { exact: true })).toBeVisible();
    await expect
      .poll(() =>
        page.evaluate(() => (window as unknown as { badgeWrites: number[] }).badgeWrites.at(-1))
      )
      .toBe(2);
    // Client navigation keeps the timer runtime and actual wake-lock status.
    await openShoppingFromCooking(page);
    await expect(page.getByRole('heading', { name: 'Shopping', level: 1 })).toBeVisible();
    await expect(page.getByRole('img', { name: 'Screen stays awake' })).toBeVisible();
    await expect
      .poll(() =>
        page.evaluate(() => (window as unknown as { badgeWrites: number[] }).badgeWrites.at(-1))
      )
      .toBe(2);
    await page.getByRole('button', { name: 'Stop cooking', exact: true }).click();
    await expect(page.locator('html')).not.toHaveAttribute('data-kitchen-lighting');
    await expect
      .poll(() =>
        page.evaluate(() => (window as unknown as { badgeWrites: number[] }).badgeWrites.at(-1))
      )
      .toBe(1);
    await page.getByRole('checkbox').first().click();
    await expect
      .poll(() =>
        page.evaluate(() => (window as unknown as { badgeWrites: number[] }).badgeWrites.at(-1))
      )
      .toBe(0);
  });
  test('German kitchen lighting controls fit a 320px screen', async ({ page }) => {
    await page.setViewportSize({ width: 320, height: 720 });
    await responsiveData(page, 'de');
    await page.goto(`/recipes/${recipeId}/cook`);
    const kitchen = page.getByRole('button', { name: 'Küchensteuerung' });
    await expect(kitchen).toBeInViewport();
    await kitchen.click();
    const display = page.getByLabel('Küchenanzeige');
    await expect(display).toBeVisible();
    for (const mode of ['glare', 'oled', 'normal']) {
      await display.selectOption(mode);
      await expectReflow(page);
      await expect(display).toBeVisible();
    }
    await expect(page.locator('html')).not.toHaveAttribute('data-kitchen-lighting');
  });
  test('updates the shopping badge without an active cooking session', async ({ page }) => {
    await responsiveData(page, 'en', { activeCooking: false });
    await page.addInitScript(() => {
      const state = window as unknown as { badgeWrites: number[] };
      state.badgeWrites = [];
      Object.defineProperty(navigator, 'setAppBadge', {
        configurable: true,
        value: async (count: number) => {
          state.badgeWrites.push(count);
        }
      });
      Object.defineProperty(navigator, 'clearAppBadge', {
        configurable: true,
        value: async () => {
          state.badgeWrites.push(0);
        }
      });
    });
    await page.goto('/shopping');
    await expect(page.getByRole('heading', { name: 'Shopping', level: 1 })).toBeVisible();
    await expect
      .poll(() =>
        page.evaluate(() => (window as unknown as { badgeWrites: number[] }).badgeWrites.at(-1))
      )
      .toBe(1);
  });
});
