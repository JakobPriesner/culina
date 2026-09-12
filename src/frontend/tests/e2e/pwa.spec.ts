import { expect, test, type Page } from '@playwright/test';

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

  test('keeps nothing from the API, on any device anyone shares', async ({ page }) => {
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

    expect(cached.filter((path) => path.startsWith('/api'))).toEqual([]);
    // And it did cache the thing that makes the app open at all.
    expect(cached).toContain('/');
  });
});
