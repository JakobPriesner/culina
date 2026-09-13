import { defineConfig, devices } from '@playwright/test';

/**
 * End-to-end tests run against the built SPA, not the dev server: the dev
 * server transforms modules on demand and hides exactly the problems — a broken
 * import, a missing asset, a service worker that never registers — these tests
 * exist to catch.
 *
 * `API_URL` points the preview server's /api proxy at a running backend. Without
 * it only the suites tagged @offline are meaningful, so the rest are skipped
 * rather than failing for the wrong reason.
 */
const port = 4173;

export default defineConfig({
  testDir: 'tests/e2e',
  // Opens registration once, so the per-flow accounts can be created without
  // every worker asking the administrator the same question.
  globalSetup: './tests/e2e/support/globalSetup.ts',
  fullyParallel: true,
  forbidOnly: Boolean(process.env.CI),
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [['github'], ['html', { open: 'never' }]] : 'list',

  use: {
    baseURL: `http://localhost:${port}`,
    trace: 'on-first-retry',
    // A real timezone and locale, so a date rendered in a test is the date a
    // person in Germany would see.
    locale: 'de-DE',
    timezoneId: 'Europe/Berlin'
  },

  projects: [
    { name: 'desktop', use: devices['Desktop Chrome'] },
    // Culina is used one-handed on a counter with wet fingers. The mobile
    // project is not optional coverage.
    { name: 'mobile', use: devices['Pixel 7'] }
  ],

  webServer: {
    // The gallery is built in for these tests only: focus trapping, the top
    // layer and light dismiss are browser behaviour, and the components that
    // rely on them have nowhere else to be exercised until the features that
    // use them exist.
    command: `VITE_GALLERY=1 pnpm build && pnpm preview --port ${port} --strictPort`,
    url: `http://localhost:${port}`,
    reuseExistingServer: !process.env.CI,
    timeout: 120_000
  }
});
