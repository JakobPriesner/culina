import { expect, test, type Page } from '@playwright/test';

/**
 * The whole first-run path, against a real backend: create an account, get a
 * household, sign out, sign back in, and follow a deep link.
 *
 * Skipped without an administrator to borrow, because the instance has to be
 * told to accept new accounts before any of this is possible — and a test that
 * quietly passes with no backend is worse than one that says it did not run.
 */
const adminEmail = process.env['CULINA_E2E_EMAIL'];
const adminPassword = process.env['CULINA_E2E_PASSWORD'];

async function signIn(page: Page, email: string, password: string) {
  await page.getByLabel(/email|e-mail/i).fill(email);
  await page.getByLabel(/password|passwort/i).fill(password);
  await page.getByRole('button', { name: /^(sign in|anmelden)$/i }).click();
}

/**
 * Borrows the browser's own session — `page.request` shares its cookie jar,
 * which the test runner's top-level `request` fixture does not.
 */
async function openRegistration(page: Page) {
  const cookies = await page.context().cookies();
  const csrf = cookies.find((cookie) => cookie.name === 'culina.csrf')?.value ?? '';

  const response = await page.request.put('/api/v1/settings/registration', {
    headers: { 'X-Culina-CSRF': csrf, Origin: new URL(page.url()).origin },
    data: { openRegistration: true, requireInvitation: false, maxUsers: 100 }
  });

  expect(response.ok(), await response.text()).toBe(true);
}

test.describe('the first-run path', () => {
  test.skip(
    !adminEmail || !adminPassword,
    'Set CULINA_E2E_EMAIL and CULINA_E2E_PASSWORD to an ADMINISTRATOR account, ' +
      'with a backend running: this suite opens registration first.'
  );

  test('register, get a household, sign out, sign in, follow a deep link', async ({ page }) => {
    // A different address every run, so the test does not depend on the state
    // the last one left behind.
    const email = `e2e-${Date.now()}-${Math.random().toString(36).slice(2, 8)}@example.test`;
    const password = 'a sentence nobody else would pick';

    await page.goto('/login');
    await signIn(page, adminEmail!, adminPassword!);
    await expect(page).toHaveURL(/\/$/);

    await openRegistration(page);

    await page.goto('/me');
    await page.getByRole('button', { name: /sign out|abmelden/i }).click();
    await expect(page).toHaveURL(/\/login/);

    // Register.
    await page.goto('/register');
    await page.getByLabel(/call you|nennen/i).fill('Sam');
    await page.getByLabel(/email|e-mail/i).fill(email);
    await page.getByLabel(/password|passwort/i).fill(password);
    await page.getByRole('button', { name: /create account|konto anlegen/i }).click();

    // No household yet, so this must not be a dead end.
    await expect(page).toHaveURL(/\/welcome$/);

    await page.getByLabel(/name your household|heißen/i).fill('Sam’s kitchen');
    await page.getByRole('button', { name: /^(create a household|haushalt anlegen)$/i }).click();

    await expect(page).toHaveURL(/\/$/);

    // Sign out, and come back through a deep link.
    await page.goto('/me');
    await page.getByRole('button', { name: /sign out|abmelden/i }).click();

    await page.goto('/shopping');
    await expect(page).toHaveURL(/\/login\?next=%2Fshopping/);

    await signIn(page, email, password);

    await expect(page).toHaveURL(/\/shopping$/);
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
  });
});
