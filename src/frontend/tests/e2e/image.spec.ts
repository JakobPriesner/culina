import { expect, test } from '@playwright/test';

/**
 * The document the .NET host serves, under its CSP; `vite preview` sets none, so nothing else
 * catches a refused boot script. Needs `CULINA_IMAGE_URL` (e.g. http://localhost:8080).
 */
const image = process.env['CULINA_IMAGE_URL'];

test.describe('the shipped image @image', () => {
  test.skip(!image, 'Set CULINA_IMAGE_URL to the address of a running image.');

  for (const path of ['/', '/recipes/new']) {
    test(`boots ${path} behind its own policy`, async ({ page }) => {
      const failures: string[] = [];

      page.on('pageerror', (error) => failures.push(error.message));
      // Listening from before the first script: the refused boot script is the violation that
      // matters, and it happens first.
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

      // Proves this is the host's document, not a server that sets no policy.
      expect(response?.headers()['content-security-policy']).toContain("'nonce-");

      await expect(
        page.getByRole('heading', { level: 1, name: /anmelden|sign in|familie|family/i })
      ).toBeVisible();
      // SvelteKit's route announcer mounts after the heading, and a refused style on it is reported
      // then.
      await page.waitForLoadState('networkidle');

      expect(failures, 'the page was refused or threw while booting').toEqual([]);
    });
  }
});
