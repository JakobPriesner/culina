import { expect, test, type Page } from '@playwright/test';

import { needsBackend, skipReason } from './support/culina';

/**
 * The whole first-run path, against a real backend: create an account, get a
 * household, sign out, sign back in, and follow a deep link.
 *
 * The only suite that makes an account of its own for every run, because what
 * it is testing is what happens to somebody who has never been here.
 */
async function signIn(page: Page, email: string, password: string) {
  await page.getByLabel(/email|e-mail/i).fill(email);
  await page.getByRole('textbox', { name: /password|passwort/i }).fill(password);
  await page.getByRole('button', { name: /^(sign in|anmelden)$/i }).click();
}

test.describe('the first-run path', () => {
  test.skip(needsBackend, skipReason);

  test('register, get a household, sign out, sign in, follow a deep link', async ({ page }) => {
    // A different address every run, so the test does not depend on the state
    // the last one left behind.
    const email = `e2e-${Date.now()}-${Math.random().toString(36).slice(2, 8)}@example.test`;
    const password = 'a sentence nobody else would pick';

    // Register. The instance was told to accept new accounts once, for the
    // whole run, in globalSetup.
    await page.goto('/register');
    await page.getByLabel(/call you|nennen/i).fill('Sam');
    await page.getByLabel(/email|e-mail/i).fill(email);
    await page.getByRole('textbox', { name: /password|passwort/i }).fill(password);
    await page.getByRole('button', { name: /create account|konto erstellen/i }).click();

    // No household yet, so this must not be a dead end.
    await expect(page).toHaveURL(/\/welcome$/);

    await page.getByLabel(/name your household|heißen/i).fill('Sam’s kitchen');
    await page.getByRole('button', { name: /^(create a household|haushalt erstellen)$/i }).click();

    await expect(page).toHaveURL(/\/$/);

    // Sign out, and come back through a deep link.
    await page.goto('/me');
    await page.getByRole('button', { name: /sign out|abmelden/i }).click();

    // Waited for: signing out is a request, and a deep link followed before it
    // lands is a deep link followed while still signed in.
    await expect(page).toHaveURL(/\/login/);

    await page.goto('/shopping');
    await expect(page).toHaveURL(/\/login\?next=%2Fshopping/);

    await signIn(page, email, password);

    await expect(page).toHaveURL(/\/shopping$/);
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
  });
});
