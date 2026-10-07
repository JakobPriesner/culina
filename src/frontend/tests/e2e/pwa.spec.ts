import { expect, test, type Page } from '@playwright/test';

import {
  accountFor,
  ensureAccount,
  needsBackend,
  signInWithHousehold,
  skipReason
} from './support/culina';

/** Install, offline open and no kept personal data; against the built app, since the dev server has no service worker. */
const activeWorkerState = (page: Page) =>
  page.evaluate(async () => {
    const registration = await navigator.serviceWorker.ready;

    return registration.active?.state ?? 'none';
  });

test.describe('the service worker @offline', () => {
  // Chromium only: other engines need their own worker setup; Safari is checked by hand.
  test.skip(({ browserName }) => browserName !== 'chromium', 'Chromium only.');

  test('takes control, and opens the app with the network gone', async ({ page, context }) => {
    await page.goto('/');

    // `ready` resolves as the worker takes over; it may still be activating, which is fine.
    expect(['activating', 'activated']).toContain(await activeWorkerState(page));

    // A worker controls a page from the *next* load onwards.
    await page.reload();

    await expect
      .poll(() => page.evaluate(() => Boolean(navigator.serviceWorker.controller)))
      .toBe(true);

    await context.setOffline(true);

    await page.goto('/recipes');

    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();

    await context.setOffline(false);
  });

  test('keeps nothing from the API that anyone did not ask it to', async ({ page }) => {
    await page.goto('/');
    await activeWorkerState(page);

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

    // Reading a recipe is the one deliberate exception to "no API responses cached".
    expect(cached.filter((path) => path.startsWith('/api'))).toEqual([]);
    expect(cached).toContain('/');
  });
});

/** The offline badge: a statement, not an alarm; it lives in the signed-in shell, so this needs an account. */
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

/** Offline depth for v1: an opened recipe stays readable (no offline editing). */
test.describe('a recipe already opened', () => {
  test.skip(({ browserName }) => browserName !== 'chromium', 'Chromium only.');
  test.skip(needsBackend, skipReason);

  test('is still readable with the network gone', async ({ browser, page, context }, testInfo) => {
    await signInWithHousehold(page, await accountFor(browser, testInfo));

    const title = `Offline ${Date.now().toString(36)}`;
    const recipeId = await writeRecipe(page, title);

    // The worker must be in control before it can answer.
    await page.evaluate(() => navigator.serviceWorker.ready.then(() => undefined));
    await page.reload();
    await expect
      .poll(() => page.evaluate(() => Boolean(navigator.serviceWorker.controller)))
      .toBe(true);

    await page.goto(`/recipes/${recipeId}`);
    await expect(page.getByRole('heading', { level: 1 })).toHaveText(title);

    await context.setOffline(true);

    await page.goto(`/recipes/${recipeId}`);

    await expect(page.getByRole('heading', { level: 1 })).toHaveText(title);
    await expect(
      page.getByRole('region', { name: /zutaten|ingredients/i }).getByText('Butter')
    ).toBeVisible();

    await context.setOffline(false);

    // Only the allow-listed shapes; the allow-list is the privacy argument.
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

    await page.goto('/');
    await expect.poll(() => cachedApiPaths(page).then((paths) => paths.length)).toBeGreaterThan(0);

    await page.goto('/me');
    await page.getByRole('button', { name: /sign out|abmelden/i }).click();
    await expect(page).toHaveURL(/\/login/);

    await expect.poll(() => cachedApiPaths(page)).toEqual([]);
  });
});

/** A shared device: what one person read is not the next one's to see. */
test.describe('a device two people use', () => {
  test.skip(({ browserName }) => browserName !== 'chromium', 'Chromium only.');
  test.skip(needsBackend, skipReason);

  test('keeps nothing of the first person for the second', async ({ browser, page }, testInfo) => {
    const first = await accountFor(browser, testInfo);
    const second = await ensureAccount(browser, `second-${testInfo.project.name}`);

    await signInWithHousehold(page, first);

    await page.evaluate(() => navigator.serviceWorker.ready.then(() => undefined));
    await page.reload();
    await expect
      .poll(() => page.evaluate(() => Boolean(navigator.serviceWorker.controller)))
      .toBe(true);

    await page.goto('/');
    await expect.poll(() => cachedApiPaths(page).then((paths) => paths.length)).toBeGreaterThan(0);

    // The first person leaves without signing out, hence sign-in clears the cache too.
    await page.goto('/login');
    await page.getByLabel(/email|e-mail/i).fill(second.email);
    await page.getByRole('textbox', { name: /password|passwort/i }).fill(second.password);
    await page.getByRole('button', { name: /^(sign in|anmelden)$/i }).click();

    await expect(page).toHaveURL(/\/(welcome)?$/);

    await expect.poll(() => cachedApiPaths(page)).not.toContain('/api/v1/recipes');
  });
});

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

test.describe('receiving a recipe from the share sheet @offline', () => {
  test.skip(({ browserName }) => browserName !== 'chromium', 'Chromium only.');

  test('stores the caption, URL and actual image before redirecting through sign-in', async ({
    page
  }) => {
    await page.goto('/');
    await activeWorkerState(page);
    await page.reload();
    await expect.poll(() => page.evaluate(() => !!navigator.serviceWorker.controller)).toBe(true);
    // A navigation POST, just as the installed app receives from the OS.
    await page.evaluate(() => {
      const form = document.createElement('form');
      form.method = 'POST';
      form.enctype = 'multipart/form-data';
      form.action = '/recipes/import';
      for (const [name, value] of Object.entries({
        title: 'Beans',
        text: '120 g beans #ad',
        url: 'https://example.com/beans'
      })) {
        const field = document.createElement('input');
        field.name = name;
        field.value = value;
        form.append(field);
      }
      const photos = document.createElement('input');
      photos.type = 'file';
      photos.name = 'photos';
      const transfer = new DataTransfer();
      transfer.items.add(
        new File([new Uint8Array([137, 80, 78, 71])], 'recipe.png', { type: 'image/png' })
      );
      photos.files = transfer.files;
      form.append(photos);
      document.body.append(form);
      form.submit();
    });
    await expect(page).toHaveURL(/(?:share=|share%3D)/);
    const read = () =>
      page.evaluate(
        () =>
          new Promise<{ text: string; url: string; bytes: number; name: string }>(
            (resolve, reject) => {
              const opening = indexedDB.open('culina-recipe-intake', 1);
              opening.onerror = () => reject(opening.error);
              opening.onsuccess = () => {
                const request = opening.result.transaction('shares').objectStore('shares').getAll();
                request.onsuccess = () => {
                  const share = request.result[0];
                  resolve({
                    text: share.text,
                    url: share.url,
                    bytes: share.photos[0].size,
                    name: share.photos[0].name
                  });
                  opening.result.close();
                };
                request.onerror = () => reject(request.error);
              };
            }
          )
      );
    const source = await read();
    expect(source).toEqual({
      text: '120 g beans #ad',
      url: 'https://example.com/beans',
      bytes: 4,
      name: 'recipe.png'
    });
    await page.reload();
    expect(await read()).toEqual(source);
  });
});
