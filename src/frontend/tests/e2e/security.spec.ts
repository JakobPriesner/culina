import { expect, test, type Page } from '@playwright/test';

import { needsBackend, signInWithHousehold, skipReason, unique } from './support/culina';

/**
 * The properties the whole auth design rests on, asserted against the built app
 * rather than against the middleware in isolation.
 *
 * A unit test of a guard proves the guard works. These prove it is wired into
 * the pipeline it ships in, in the order the pipeline promises — which is the
 * part that a refactor can quietly undo.
 */
test.describe.configure({ mode: 'serial' });

test.describe('the way in', () => {
  test.skip(needsBackend, skipReason);

  let page: Page;

  test.beforeAll(async ({ browser }) => {
    if (needsBackend) {
      return;
    }

    page = await browser.newPage();

    await signInWithHousehold(page);
  });

  test.afterAll(async () => {
    await page?.close();
  });

  test('refuses a change that carries no CSRF token', async () => {
    const origin = new URL(page.url()).origin;
    const household = (await (await page.request.get('/api/v1/users/me')).json()).households[0]
      .householdId;

    // The session cookie goes along — `page.request` shares the jar — so this
    // is a request that is authenticated and still must not be honoured. That
    // is the whole point: a cookie proves who you are, never that you asked.
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

    // Token and cookie both correct, and the request still does not come from
    // us. Per-IP rate limiting and the token are not enough on their own; the
    // origin check is what makes a stolen token unusable from a page an
    // attacker controls.
    const elsewhere = await page.request.post('/api/v1/recipes', {
      headers: { Origin: 'https://not-culina.example', 'X-Culina-CSRF': csrf ?? '' },
      data: { householdId: household, title: unique('Elsewhere') }
    });

    expect(elsewhere.status()).toBe(403);
  });

  test('sends a stranger to sign in, and then where they were going', async ({ browser }) => {
    const stranger = await browser.newContext();
    const strangersPage = await stranger.newPage();

    await strangersPage.goto('/shopping');

    // Not a bare redirect to the front page: somebody who followed a link to a
    // recipe should land on that recipe, not be made to find it again.
    await expect(strangersPage).toHaveURL(/\/login\?next=%2Fshopping/);

    await stranger.close();
  });

  test('leaves nothing behind when a session ends', async ({ browser }) => {
    const context = await browser.newContext();
    const theirs = await context.newPage();

    await signInWithHousehold(theirs);
    await theirs.goto('/me');
    await theirs.getByRole('button', { name: /sign out|abmelden/i }).click();
    await expect(theirs).toHaveURL(/\/login/);

    // The cookie is gone, not merely ignored.
    const cookies = await context.cookies();

    expect(cookies.find((cookie) => cookie.name === '__Host-culina.session')?.value ?? '').toBe('');

    // And the app does not let the back button show the previous person's data.
    await theirs.goto('/');
    await expect(theirs).toHaveURL(/\/login/);

    await context.close();
  });
});
