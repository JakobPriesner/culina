import { expect, test } from '@playwright/test';

import { responsiveData } from './support/responsive';

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
    // The sign-in page, because appearance and language must be changeable
    // before there is an account to store them against.
    await page.goto('/login');

    const toggle = page.getByRole('button', { name: /appearance|darstellung/i });

    await toggle.click();

    const chosen = await page.locator('html').getAttribute('data-mode');

    await page.reload();

    await expect(page.locator('html')).toHaveAttribute('data-mode', chosen!);
  });

  test('switches language without reloading', async ({ page }) => {
    await page.goto('/login');

    const picker = page.getByRole('combobox');

    await picker.selectOption('de');
    await expect(page.locator('html')).toHaveAttribute('lang', 'de');
    await expect(page.getByRole('heading', { name: 'Anmelden' })).toBeVisible();

    await picker.selectOption('en');
    await expect(page.locator('html')).toHaveAttribute('lang', 'en');
    await expect(page.getByRole('heading', { name: 'Sign in' })).toBeVisible();
  });

  /*
   * Settings shows all three modes at once rather than the header's cycling
   * button: the question there is "which of these is it", and an icon that has
   * to be pressed twice to answer that is not an answer. Which makes the state
   * of the other two radios part of the control, not decoration.
   */
  test('chooses a mode outright in settings', async ({ page }) => {
    await responsiveData(page, 'en');
    await page.goto('/me/appearance');

    const theme = page.getByRole('group', { name: /^theme$/i });
    const dark = theme.getByRole('radio', { name: /^dark$/i });

    await expect(theme.getByRole('radio', { name: /^light$/i })).toBeChecked();

    // Clicked the way a person does — on the segment, not on the radio clipped
    // underneath it.
    await theme.getByText(/^dark$/i).click();

    await expect(page.locator('html')).toHaveAttribute('data-mode', 'dark');
    await expect(dark).toBeChecked();
    await expect(theme.getByRole('radio', { name: /^light$/i })).not.toBeChecked();
  });
});
