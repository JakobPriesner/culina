import { expect, test } from '@playwright/test';

import { responsiveData } from './support/responsive';

/** The flash of the wrong theme is the bug this prevents, so check the appearance was applied before the document finished parsing. */
test.describe('appearance @offline', () => {
  test('applies the stored theme before the document is ready', async ({ page }) => {
    await page.addInitScript(() => {
      localStorage.setItem(
        'culina.appearance',
        JSON.stringify({ theme: 'warm-paper', mode: 'dark' })
      );

      // Recorded before anything the app renders: if the inline script did not run first, there is nothing to read.
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
    // The sign-in page: appearance and language must be changeable before any account exists.
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

  /* Settings shows all three modes at once, since the header's cycling button makes "which is it" take two presses. */
  test('chooses a mode outright in settings', async ({ page }) => {
    await responsiveData(page, 'en');
    await page.goto('/me/appearance');

    const theme = page.getByRole('group', { name: /^theme$/i });
    const dark = theme.getByRole('radio', { name: /^dark$/i });

    await expect(theme.getByRole('radio', { name: /^light$/i })).toBeChecked();

    // Clicked as a person does: on the segment, not the radio clipped beneath it.
    await theme.getByText(/^dark$/i).click();

    await expect(page.locator('html')).toHaveAttribute('data-mode', 'dark');
    await expect(dark).toBeChecked();
    await expect(theme.getByRole('radio', { name: /^light$/i })).not.toBeChecked();
  });

  /* Apple's glass gives way to the person's display settings; so does Culina's, before the first paint. */
  test('Increase Contrast makes the glass solid and the theme stronger', async ({ page }) => {
    test.skip(test.info().project.name !== 'desktop', 'The header pills are the wide layout.');
    await responsiveData(page, 'en');
    await page.goto('/');

    const html = page.locator('html');
    const nav = page.locator('nav.top');

    // Glass to begin with, so the solid one below is the setting's doing.
    await expect(nav).toHaveCSS('backdrop-filter', /blur/);

    await page.emulateMedia({ contrast: 'more' });
    await expect(html).toHaveAttribute('data-contrast', 'more');
    await expect(nav).toHaveCSS('backdrop-filter', 'none');
    // The theme's own Increase Contrast block: muted text a step darker than the usual sand-600.
    await expect
      .poll(() => html.evaluate((root) => getComputedStyle(root).getPropertyValue('--text-muted')))
      .toBe('#443f36');

    await page.emulateMedia({ contrast: 'no-preference' });
    await expect(html).toHaveAttribute('data-contrast', 'normal');
    await expect(nav).toHaveCSS('backdrop-filter', /blur/);
  });

  test('Reduce Transparency is applied before the document is ready', async ({ page }) => {
    await page.addInitScript(() => {
      // No browser emulates Reduce Transparency yet: answer the query as a device with it on would.
      const real = window.matchMedia.bind(window);

      window.matchMedia = (query: string) =>
        query.includes('prefers-reduced-transparency')
          ? ({ ...real(query), matches: true, media: query } as MediaQueryList)
          : real(query);
      document.addEventListener('DOMContentLoaded', () => {
        Object.assign(window, {
          transparencyWhenReady: document.documentElement.dataset['transparency']
        });
      });
    });
    await responsiveData(page, 'en');
    await page.goto('/');

    await expect
      .poll(() =>
        page.evaluate(
          () => (window as unknown as { transparencyWhenReady?: string }).transparencyWhenReady
        )
      )
      .toBe('reduced');
    await expect(page.locator('nav.nav:visible')).toHaveCSS('backdrop-filter', 'none');
  });
});
