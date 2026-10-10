import { expect, test } from '@playwright/test';

/**
 * The app under the document policy's Trusted Types rules.
 *
 * `vite preview` sets no policy, so nothing else here would notice a string
 * reaching `innerHTML` or the worker's registration — the browser would simply
 * accept it. This serves the built document with the directives the host sends
 * (`SecurityHeaders.DocumentContentSecurityPolicy`) and fails on any violation
 * or on a worker that never registers.
 */
const policy = (nonce: string) =>
  [
    "default-src 'self'",
    `script-src 'self' 'nonce-${nonce}'`,
    `style-src 'self' 'nonce-${nonce}'`,
    "style-src-attr 'unsafe-hashes' 'sha256-S8qMpvofolR8Mpjy4kQvEm7m1q8clzU4dfDH0AmvZjo='",
    "img-src 'self' data: blob:",
    "connect-src 'self'",
    "font-src 'self'",
    "manifest-src 'self'",
    "worker-src 'self'",
    "object-src 'none'",
    "base-uri 'none'",
    "require-trusted-types-for 'script'",
    'trusted-types svelte-trusted-html sveltekit-trusted-url culina-worker-url'
  ].join('; ');

test.describe('trusted types @offline', () => {
  test('boots and registers its worker without a string reaching a script sink', async ({
    page
  }) => {
    const failures: string[] = [];

    page.on('pageerror', (error) => failures.push(error.message));
    page.on('console', (message) => {
      if (
        /Trusted(HTML|Script|ScriptURL|Types)|violates.*Content Security Policy/i.test(
          message.text()
        )
      ) {
        failures.push(message.text());
      }
    });
    await page.addInitScript(() => {
      document.addEventListener('securitypolicyviolation', (event) => {
        console.error(`csp: ${event.violatedDirective} blocked ${event.blockedURI}`);
      });
    });

    await page.route(/^http:\/\/localhost:4173\/(?:login)?(?:\?.*)?$/, async (route) => {
      const response = await route.fetch();
      const nonce = 'trustedTypesTest';

      await route.fulfill({
        response,
        body: (await response.text())
          .replaceAll('<script>', '<script nonce="__CULINA_NONCE__">')
          .replaceAll('__CULINA_NONCE__', nonce),
        headers: { ...response.headers(), 'Content-Security-Policy': policy(nonce) }
      });
    });

    await page.goto('/');
    await expect(
      page.getByRole('heading', { level: 1, name: /anmelden|sign in|familie|family/i })
    ).toBeVisible();

    // Registration is the sink the app owns: a plain string there is refused,
    // and the worker never appears.
    await expect
      .poll(() =>
        page.evaluate(async () => Boolean(await navigator.serviceWorker.getRegistration()))
      )
      .toBe(true);

    await page.waitForLoadState('networkidle');

    expect(failures, 'a Trusted Types or policy violation').toEqual([]);
  });
});
