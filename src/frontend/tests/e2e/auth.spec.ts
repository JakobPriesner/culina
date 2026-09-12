import { expect, test } from '@playwright/test';

/**
 * The deep-link path, against a real backend.
 *
 * Skipped without `CULINA_E2E_EMAIL`, because there is nothing honest to assert
 * about signing in when there is nothing to sign in to — and a test that quietly
 * passes in that case is worse than one that says it did not run.
 */
const email = process.env['CULINA_E2E_EMAIL'];
const password = process.env['CULINA_E2E_PASSWORD'];

test.describe('signing in', () => {
  test.skip(
    !email || !password,
    'Set CULINA_E2E_EMAIL and CULINA_E2E_PASSWORD with a backend running.'
  );

  test('sends a deep link through login and back to where it was going', async ({ page }) => {
    await page.goto('/shopping');

    await expect(page).toHaveURL(/\/login\?next=%2Fshopping/);

    await page.getByLabel(/email|e-mail/i).fill(email!);
    await page.getByLabel(/password|passwort/i).fill(password!);
    await page.getByRole('button', { name: /sign in|anmelden/i }).click();

    await expect(page).toHaveURL(/\/shopping$/);
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
  });

  test('leaves nothing behind after signing out', async ({ page }) => {
    await page.goto('/login');

    await page.getByLabel(/email|e-mail/i).fill(email!);
    await page.getByLabel(/password|passwort/i).fill(password!);
    await page.getByRole('button', { name: /sign in|anmelden/i }).click();

    await expect(page).toHaveURL(/\/$/);

    await page.goto('/me');
    await page.getByRole('button', { name: /sign out|abmelden/i }).click();

    await expect(page).toHaveURL(/\/login/);

    // Back into a protected route: the guard must ask again rather than find a
    // session still sitting in memory.
    await page.goto('/me');

    await expect(page).toHaveURL(/\/login/);
  });
});
