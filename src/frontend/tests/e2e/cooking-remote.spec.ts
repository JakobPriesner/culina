import { expect, test } from '@playwright/test';
import { expectReflow, recipeId, responsiveData } from './support/responsive';

type RemoteHarness = Window & { remoteAction: (action: MediaSessionAction) => void };

test.describe('cooking remote @offline', () => {
  test.use({ serviceWorkers: 'block' });

  test('plays its real audio under CSP and shares steps, timers and cleanup across routes', async ({
    page
  }, testInfo) => {
    await responsiveData(page, 'en');
    await page.route(`**/api/v1/recipes/${recipeId}`, (route) =>
      route.fulfill({
        json: {
          recipeId,
          householdId: '00000000-0000-4000-8000-000000000002',
          title: 'Butter sauce',
          language: 'en',
          yieldAmount: 2,
          yieldKind: 'servings',
          groups: [],
          tags: [],
          version: 1,
          steps: Array.from({ length: 4 }, (_, index) => ({
            stepId: `s${index}`,
            title: index === 0 ? 'Simmer' : null,
            durationSeconds: index === 0 ? 120 : null,
            uses: [],
            segments: [{ type: 'text', value: index === 0 ? 'Simmer gently.' : 'Stir and taste.' }]
          }))
        },
        headers: { ETag: '"v1"' }
      })
    );
    const policyErrors: string[] = [];
    page.on('console', (message) => {
      if (/violates.*Content Security Policy|Refused.*policy/i.test(message.text()))
        policyErrors.push(message.text());
    });
    await page.route(new RegExp(`/recipes/${recipeId}/cook(?:\\?.*)?$`), async (route) => {
      const response = await route.fetch();
      await route.fulfill({
        response,
        body: (await response.text())
          .replaceAll('<script>', '<script nonce="__CULINA_NONCE__">')
          .replaceAll('__CULINA_NONCE__', 'remoteTest'),
        headers: {
          ...response.headers(),
          'Content-Security-Policy':
            "default-src 'self'; script-src 'self' 'nonce-remoteTest'; style-src 'self' 'nonce-remoteTest'; style-src-attr 'unsafe-hashes' 'sha256-S8qMpvofolR8Mpjy4kQvEm7m1q8clzU4dfDH0AmvZjo='; img-src 'self' data: blob:; base-uri 'none'; object-src 'none'"
        }
      });
    });
    await page.addInitScript(() => {
      const handlers: Partial<Record<MediaSessionAction, MediaSessionActionHandler | null>> = {};
      const register = navigator.mediaSession.setActionHandler.bind(navigator.mediaSession);
      navigator.mediaSession.setActionHandler = (action, handler) => {
        handlers[action] = handler;
        register(action, handler);
      };
      (window as unknown as RemoteHarness).remoteAction = (action) =>
        handlers[action]?.({ action });
      localStorage.setItem(
        'culina.timers.session-1',
        JSON.stringify([{ stepIndex: 0, endsAt: Date.now() + 600000, label: 'Simmer' }])
      );
    });
    await page.goto(`/recipes/${recipeId}/cook`);
    const asset = page.waitForResponse((response) =>
      /cooking-silence[^/]*\.wav/.test(response.url())
    );
    const kitchen = page.getByRole('button', { name: 'Kitchen controls', exact: true });
    await expect(kitchen).toBeInViewport();
    await kitchen.click();
    await page.getByRole('button', { name: 'Remote controls', exact: true }).click();
    expect((await asset).ok()).toBe(true);
    await expect(page.getByRole('button', { name: 'Turn off remote' })).toBeVisible();
    await page
      .getByRole('dialog', { name: 'Kitchen controls' })
      .getByRole('button', { name: 'Close', exact: true })
      .click();
    await expect
      .poll(() => page.evaluate(() => navigator.mediaSession.metadata?.title))
      .toContain('Step 1 of 4');
    await page.evaluate(() => (window as unknown as RemoteHarness).remoteAction('pause'));
    await expect(page.getByRole('button', { name: 'Resume timer' })).toBeVisible();
    await page.evaluate(() => (window as unknown as RemoteHarness).remoteAction('play'));
    await expect(page.getByRole('button', { name: 'Pause timer' })).toBeVisible();
    await page.evaluate(() => (window as unknown as RemoteHarness).remoteAction('nexttrack'));
    await expect(page.getByText('Step 2 of 4', { exact: true })).toBeVisible();
    await expectReflow(page);
    await page.screenshot({ path: testInfo.outputPath('cooking-remote.png'), fullPage: true });
    await page
      .locator('nav:visible')
      .getByRole('link', { name: /shopping/i })
      .click();
    await expect(page.getByRole('heading', { name: 'Shopping', level: 1 })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Turn off remote' })).toBeVisible();
    await page.evaluate(() => (window as unknown as RemoteHarness).remoteAction('nexttrack'));
    await expect
      .poll(() => page.evaluate(() => navigator.mediaSession.metadata?.title))
      .toContain('Step 3 of 4');
    await page.getByRole('link', { name: /keep cooking/i }).click();
    await expect(page.getByText('Step 3 of 4', { exact: true })).toBeVisible();
    await page.evaluate(() => (window as unknown as RemoteHarness).remoteAction('previoustrack'));
    await expect(page.getByText('Step 2 of 4', { exact: true })).toBeVisible();
    await page
      .locator('nav:visible')
      .getByRole('link', { name: /shopping/i })
      .click();
    await page.getByRole('button', { name: 'Stop cooking', exact: true }).click();
    await expect.poll(() => page.evaluate(() => navigator.mediaSession.metadata)).toBeNull();
    await expect.poll(() => page.evaluate(() => navigator.mediaSession.playbackState)).toBe('none');
    expect(policyErrors).toEqual([]);
  });
});

test('the cooking audio is installed and decodes offline through the service worker @offline', async ({
  page,
  context
}) => {
  await page.goto('/');
  await page.evaluate(async () => {
    await navigator.serviceWorker.ready;
  });
  await page.reload();
  await expect
    .poll(() => page.evaluate(() => Boolean(navigator.serviceWorker.controller)))
    .toBe(true);
  const audioUrl = await page.evaluate(async () => {
    for (const name of await caches.keys()) {
      const keys = await (await caches.open(name)).keys();
      const audio = keys.find((request) => /cooking-silence[^/]*\.wav/.test(request.url));
      if (audio) return audio.url;
    }
    return null;
  });
  expect(audioUrl).not.toBeNull();
  await context.setOffline(true);
  const duration = await page.evaluate(
    (url) =>
      new Promise<number>((resolve, reject) => {
        const audio = new Audio(url!);
        audio.onloadedmetadata = () => {
          resolve(audio.duration);
          audio.removeAttribute('src');
          audio.load();
        };
        audio.onerror = () => reject(new Error('Cooking audio failed to load offline'));
        audio.load();
      }),
    audioUrl
  );
  expect(duration).toBe(10);
  await context.setOffline(false);
});
