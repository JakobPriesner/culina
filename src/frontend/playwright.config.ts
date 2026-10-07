import { defineConfig, devices } from '@playwright/test';

/**
 * E2E runs against the built SPA, not the dev server, which hides what these tests catch (broken imports, missing assets, a service worker that never registers).
 * `CULINA_API` overrides the /api proxy target (localhost:5000). Signed-in suites need a backend and CULINA_E2E_* credentials and skip without them;
 * @offline needs only the built app; @image opens a running image at `CULINA_IMAGE_URL` under the policy the host really sends.
 */
const port = 4173;

export default defineConfig({
  testDir: 'tests/e2e',
  // Opens registration once, so per-flow accounts can be created without every worker asking the administrator.
  globalSetup: './tests/e2e/support/globalSetup.ts',
  fullyParallel: true,
  forbidOnly: Boolean(process.env.CI),
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [['github'], ['html', { open: 'never' }]] : 'list',

  use: {
    baseURL: `http://localhost:${port}`,
    trace: 'on-first-retry',
    // A real timezone and locale, so dates render as a person in Germany sees them.
    locale: 'de-DE',
    timezoneId: 'Europe/Berlin'
  },

  projects: [
    { name: 'desktop', use: devices['Desktop Chrome'] },
    // Used one-handed with wet fingers: the mobile project is not optional coverage.
    { name: 'mobile', use: devices['Pixel 7'] }
  ],

  webServer: {
    // The gallery is built in for these tests only: focus trapping, top layer and light dismiss are browser behaviour with nowhere else to be exercised yet.
    command: `VITE_GALLERY=1 pnpm build && pnpm preview --port ${port} --strictPort`,
    url: `http://localhost:${port}`,
    reuseExistingServer: !process.env.CI,
    timeout: 120_000
  }
});
