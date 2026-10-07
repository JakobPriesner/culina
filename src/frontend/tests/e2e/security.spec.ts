import { expect, test, type Page } from '@playwright/test';

import {
  accountFor,
  needsBackend,
  signInWithHousehold,
  skipReason,
  unique
} from './support/culina';

/** Auth-design properties asserted against the built app, proving the guards are wired into the shipped pipeline in the promised order. */
test.describe.configure({ mode: 'serial' });

test.describe('the way in', () => {
  test.skip(needsBackend, skipReason);

  let page: Page;

  test.beforeAll(async ({ browser }, testInfo) => {
    if (needsBackend) {
      return;
    }

    page = await browser.newPage();

    await signInWithHousehold(page, await accountFor(browser, testInfo));
  });

  test.afterAll(async () => {
    await page?.close();
  });

  test('refuses a change that carries no CSRF token', async () => {
    const origin = new URL(page.url()).origin;
    const household = (await (await page.request.get('/api/v1/users/me')).json()).households[0]
      .householdId;

    // `page.request` shares the cookie jar: authenticated, and still must not be honoured, since a cookie proves who you are, not that you asked.
    const forged = await page.request.post('/api/v1/recipes', {
      headers: { Origin: origin },
      data: { householdId: household, title: unique('Forged') }
    });

    expect(forged.status()).toBe(403);
    expect((await forged.json()).code).toBe('auth.csrf_invalid');
  });

  test('refuses a change that came from somewhere else', async () => {
    const csrf = (await page.context().cookies()).find(
      (cookie) => cookie.name === 'culina.csrf'
    )?.value;
    const household = (await (await page.request.get('/api/v1/users/me')).json()).households[0]
      .householdId;

    // Token and cookie correct but the request is not from us: the origin check makes a stolen token unusable from an attacker's page.
    const elsewhere = await page.request.post('/api/v1/recipes', {
      headers: { Origin: 'https://not-culina.example', 'X-Culina-CSRF': csrf ?? '' },
      data: { householdId: household, title: unique('Elsewhere') }
    });

    expect(elsewhere.status()).toBe(403);
  });

  test('sends a stranger to sign in, and then where they were going', async ({
    browser
  }, testInfo) => {
    const who = await accountFor(browser, testInfo);
    const stranger = await browser.newContext();
    const strangersPage = await stranger.newPage();

    await strangersPage.goto('/shopping');

    await expect(strangersPage).toHaveURL(/\/login\?next=%2Fshopping/);

    await strangersPage.getByLabel(/email|e-mail/i).fill(who.email);
    await strangersPage.getByRole('textbox', { name: /password|passwort/i }).fill(who.password);
    await strangersPage.getByRole('button', { name: /(^|\s)(sign (me )?in|anmelden)$/i }).click();

    await expect(strangersPage).toHaveURL(/\/shopping$/);
    await expect(strangersPage.getByRole('heading', { level: 1 })).toBeVisible();

    await stranger.close();
  });

  test('leaves nothing behind when a session ends', async ({ browser }, testInfo) => {
    const context = await browser.newContext();
    const theirs = await context.newPage();

    await signInWithHousehold(theirs, await accountFor(browser, testInfo));

    // Production names it __Host-culina.session; over plain HTTP, as locally, the prefix is dropped. It must exist now or the check below proves nothing.
    const sessionCookie = (await context.cookies()).find((cookie) =>
      ['__Host-culina.session', 'culina.session'].includes(cookie.name)
    );

    expect(sessionCookie?.value).toBeTruthy();

    await theirs.goto('/me');
    await theirs.getByRole('button', { name: /sign out|abmelden/i }).click();
    await expect(theirs).toHaveURL(/\/login/);

    const cookies = await context.cookies();

    expect(cookies.find((cookie) => cookie.name === sessionCookie?.name)?.value ?? '').toBe('');

    // The back button must not show the previous person's data.
    await theirs.goto('/');
    await expect(theirs).toHaveURL(/\/login/);

    await context.close();
  });
});
