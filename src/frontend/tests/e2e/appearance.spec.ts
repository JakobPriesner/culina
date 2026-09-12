import { expect, test } from '@playwright/test';

/**
 * The flash of the wrong theme is the bug this whole arrangement exists to
 * prevent, so it is tested the only way that means anything: by checking that
 * the appearance was already applied before the document finished parsing.
 */
test.describe('appearance @offline', () => {
  test('applies the stored theme before the document is ready', async ({ page }) => {
    await page.addInitScript(() => {
      localStorage.setItem(
        'culina.appearance',
        JSON.stringify({ theme: 'warm-paper', mode: 'dark' })
      );

      // Recorded before anything the app renders: if the inline script did not
      // run first, there is nothing here to read.
      document.addEventListener('DOMContentLoaded', () => {
        Object.assign(window, { modeWhenReady: document.documentElement.dataset['mode'] });
      });
    });

    await page.goto('/');

    await expect
      .poll(() =>
        page.evaluate(() => (window as unknown as { modeWhenReady?: string }).modeWhenReady)
      )
      .toBe('dark');
  });

  test('remembers a choice across a reload', async ({ page }) => {
    await page.goto('/');

    const toggle = page.getByRole('button', { name: /appearance|darstellung/i });

    await toggle.click();

    const chosen = await page.locator('html').getAttribute('data-mode');

    await page.reload();

    await expect(page.locator('html')).toHaveAttribute('data-mode', chosen!);
  });

  test('switches language without reloading', async ({ page }) => {
    await page.goto('/');

    const picker = page.getByRole('combobox');

    await picker.selectOption('de');
    await expect(page.locator('html')).toHaveAttribute('lang', 'de');
    await expect(page.getByText('Deine Rezepte, so wie du kochst.')).toBeVisible();

    await picker.selectOption('en');
    await expect(page.locator('html')).toHaveAttribute('lang', 'en');
    await expect(page.getByText('Your recipes, the way you cook them.')).toBeVisible();
  });
});
