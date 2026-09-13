import { expect, test, type Page } from '@playwright/test';

import { accountFor, needsBackend, signInWithHousehold, skipReason } from './support/culina';

/**
 * The app installs, opens without a network, and never keeps anyone's data.
 *
 * Against the built app, because a service worker does not exist under the dev
 * server: this is one of the few things that can only be wrong in production.
 */
/** Waits for the worker to be running, and reports what it is doing. */
const activeWorkerState = (page: Page) =>
  page.evaluate(async () => {
    const registration = await navigator.serviceWorker.ready;

    return registration.active?.state ?? 'none';
  });

test.describe('the service worker @offline', () => {
  // Chromium only: WebKit and Firefox need their own setup for workers under
  // test, and a phone that installs Culina is Chrome or Safari — Safari's own
  // behaviour is checked by hand, not pretended at here.
  test.skip(({ browserName }) => browserName !== 'chromium', 'Chromium only.');

  test('takes control, and opens the app with the network gone', async ({ page, context }) => {
    await page.goto('/');

    // Registration is the app's own, not the framework's, so this also proves
    // the layout wired it up.
    // `ready` resolves as the worker takes over, so it may still be finishing
    // its activation; what matters is that it is running, not which instant.
    expect(['activating', 'activated']).toContain(await activeWorkerState(page));

    // A worker controls a page from the *next* load onwards.
    await page.reload();

    await expect
      .poll(() => page.evaluate(() => Boolean(navigator.serviceWorker.controller)))
      .toBe(true);

    await context.setOffline(true);

    // A deep link, not the page that is already open: this is the tablet on
    // the counter being woken up in a kitchen the wifi does not reach.
    await page.goto('/recipes');

    // Something of Culina's own is on screen rather than the browser's error
    // page. Signed out and offline, that is the sign-in heading.
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();

    await context.setOffline(false);
  });

  test('keeps nothing from the API that anyone did not ask it to', async ({ page }) => {
    await page.goto('/');
    await activeWorkerState(page);

    // The sign-in page has already asked the server who is here, so if the
    // worker cached API responses at all, one would be in here by now.
    const cached = await page.evaluate(async () => {
      const names = await caches.keys();
      const entries = await Promise.all(
        names.map(async (name) => {
          const cache = await caches.open(name);
          const keys = await cache.keys();

          return keys.map((request) => new URL(request.url).pathname);
        })
      );

      return entries.flat();
    });

    // Reading a recipe is the one deliberate exception, and the sign-in page
    // has read none. Everything else the app asks the server — who is here,
    // what the settings are — is gone the moment the answer is used.
    expect(cached.filter((path) => path.startsWith('/api'))).toEqual([]);
    // And it did cache the thing that makes the app open at all.
    expect(cached).toContain('/');
  });
});

/**
 * The offline badge, which is the only thing the shell says about the network.
 *
 * It is a statement and not an alarm: no dialogue, nothing that takes over the
 * screen, and nothing at all when there is a connection. It lives in the
 * signed-in shell beside the navigation, so this suite needs an account.
 */
test.describe('being offline', () => {
  test.skip(({ browserName }) => browserName !== 'chromium', 'Chromium only.');
  test.skip(needsBackend, skipReason);

  test('is said once, quietly, and taken back without ceremony', async ({
    browser,
    page,
    context
  }, testInfo) => {
    await signInWithHousehold(page, await accountFor(browser, testInfo));

    const badge = page.getByText(/^offline$/i);

    await expect(badge).toHaveCount(0);

    await context.setOffline(true);

    await expect(badge).toBeVisible();

    await context.setOffline(false);

    await expect(badge).toHaveCount(0);
  });
});

/**
 * The offline depth chosen for v1: a recipe you have opened stays readable.
 *
 * Kitchens have bad wifi, and this is the scenario that actually happens —
 * someone picked the recipe on the sofa and carried the phone to a counter the
 * router does not reach. Full offline editing costs an order of magnitude more
 * and is wanted by nobody.
 */
test.describe('a recipe already opened', () => {
  test.skip(({ browserName }) => browserName !== 'chromium', 'Chromium only.');
  test.skip(needsBackend, skipReason);

  test('is still readable with the network gone', async ({ browser, page, context }, testInfo) => {
    await signInWithHousehold(page, await accountFor(browser, testInfo));

    // Its own recipe, written through the API: the test must not depend on
    // what happens to be in this household.
    const title = `Offline ${Date.now().toString(36)}`;
    const recipeId = await writeRecipe(page, title);

    // The worker has to be in control before it can answer anything.
    await page.evaluate(() => navigator.serviceWorker.ready.then(() => undefined));
    await page.reload();
    await expect
      .poll(() => page.evaluate(() => Boolean(navigator.serviceWorker.controller)))
      .toBe(true);

    // Read it once, on the sofa.
    await page.goto(`/recipes/${recipeId}`);
    await expect(page.getByRole('heading', { level: 1 })).toHaveText(title);

    await context.setOffline(true);

    // A cold open, not a page that is already rendered: this is the phone
    // being woken up at the counter.
    await page.goto(`/recipes/${recipeId}`);

    await expect(page.getByRole('heading', { level: 1 })).toHaveText(title);
    // And the ingredients with it, since a title alone cooks nothing.
    await expect(
      page.getByRole('region', { name: /zutaten|ingredients/i }).getByText('Butter')
    ).toBeVisible();

    await context.setOffline(false);

    // Only the shapes that were asked for. The allow-list is the whole of the
    // privacy argument, so it is worth a test that would notice it widening.
    const kept = await cachedApiPaths(page);

    expect(kept.length).toBeGreaterThan(0);
    expect(
      kept.filter((path) => !/^\/api\/v1\/(recipes(\/[^/]+(\/image)?)?|users\/me)$/.test(path))
    ).toEqual([]);
  });

  test('is gone from the device the moment anyone signs out', async ({
    browser,
    page
  }, testInfo) => {
    await signInWithHousehold(page, await accountFor(browser, testInfo));

    await page.evaluate(() => navigator.serviceWorker.ready.then(() => undefined));
    await page.reload();
    await expect
      .poll(() => page.evaluate(() => Boolean(navigator.serviceWorker.controller)))
      .toBe(true);

    // Read something, so there is something to leave behind.
    await page.goto('/');
    await expect.poll(() => cachedApiPaths(page).then((paths) => paths.length)).toBeGreaterThan(0);

    await page.goto('/me');
    await page.getByRole('button', { name: /sign out|abmelden/i }).click();
    await expect(page).toHaveURL(/\/login/);

    // The next person at this tablet is a different person.
    await expect.poll(() => cachedApiPaths(page)).toEqual([]);
  });
});

/** Every API path this device is currently holding on to. */
async function cachedApiPaths(page: Page): Promise<string[]> {
  return page.evaluate(async () => {
    const names = await caches.keys();
    const entries = await Promise.all(
      names.map(async (name) => {
        const cache = await caches.open(name);
        const keys = await cache.keys();

        return keys.map((request) => new URL(request.url).pathname);
      })
    );

    return entries.flat().filter((path) => path.startsWith('/api'));
  });
}

/** Writes one small recipe through the API, borrowing the browser's session. */
async function writeRecipe(page: Page, title: string): Promise<string> {
  const cookies = await page.context().cookies();
  const csrf = cookies.find((cookie) => cookie.name === 'culina.csrf')?.value ?? '';
  const headers = { 'X-Culina-CSRF': csrf, Origin: new URL(page.url()).origin };

  const me = await page.request.get('/api/v1/users/me');
  const householdId = (await me.json()).households[0].householdId;

  const created = await page.request.post('/api/v1/recipes', {
    headers,
    data: { householdId, title }
  });

  const { recipeId } = await created.json();
  const read = await page.request.get(`/api/v1/recipes/${recipeId}`);

  const saved = await page.request.put(`/api/v1/recipes/${recipeId}`, {
    headers: { ...headers, 'If-Match': read.headers()['etag']! },
    data: {
      title,
      language: 'de',
      yieldAmount: 2,
      yieldKind: 'servings',
      groups: [{ ingredients: [{ quantity: 200, unit: 'g', name: 'Butter' }] }],
      steps: [],
      tags: []
    }
  });

  expect(saved.ok(), await saved.text()).toBe(true);

  return recipeId;
}
