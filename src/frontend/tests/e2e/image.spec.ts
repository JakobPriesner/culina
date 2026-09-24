import { expect, test } from '@playwright/test';

/**
 * The document the .NET host serves, under the policy it serves it with.
 *
 * Every other spec runs against `vite preview`, which sets no Content Security
 * Policy — so nothing else ever loads the page behind `script-src 'self'
 * 'nonce-…'`. That is how SvelteKit's inline boot script went un-nonced in
 * every image and the app never started, while the whole suite stayed green.
 *
 * `CULINA_IMAGE_URL` is the running image, e.g. http://localhost:8080; without
 * it there is nothing to open, and the test says so rather than passing.
 */
const image = process.env['CULINA_IMAGE_URL'];

test.describe('the shipped image @image', () => {
  test.skip(!image, 'Set CULINA_IMAGE_URL to the address of a running image.');

  for (const path of ['/', '/recipes/new']) {
    test(`boots ${path} behind its own policy`, async ({ page }) => {
      const failures: string[] = [];

      page.on('pageerror', (error) => failures.push(error.message));
      // Listening from before the first script, because the violation that
      // matters is the boot script being refused, and it happens first.
      await page.addInitScript(() => {
        document.addEventListener('securitypolicyviolation', (event) => {
          console.error(`csp: ${event.violatedDirective} blocked ${event.blockedURI}`);
        });
      });
      page.on('console', (message) => {
        if (message.text().startsWith('csp: ')) {
          failures.push(message.text());
        }
      });

      const response = await page.goto(new URL(path, image).href);

      // Proof this is the host's document and not a server that sets no policy,
      // which would pass everything below without testing anything.
      expect(response?.headers()['content-security-policy']).toContain("'nonce-");

      // Signed out, a deep link lands on sign-in too.
      await expect(
        page.getByRole('heading', { level: 1, name: /anmelden|sign in/i })
      ).toBeVisible();
      // SvelteKit's route announcer mounts after the heading does, and a
      // refused style on it is reported then, not before.
      await page.waitForLoadState('networkidle');

      expect(failures, 'the page was refused or threw while booting').toEqual([]);
    });
  }
});
