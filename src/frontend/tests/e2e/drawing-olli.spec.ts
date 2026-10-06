import { expect, test, type Route } from '@playwright/test';
import { recipeId, responsiveData } from './support/responsive';

test.describe('drawing Olli @offline', () => {
  test.use({ serviceWorkers: 'block' });

  test('keeps drawing visible with Settings motion disabled while image generation finishes', async ({
    page
  }, testInfo) => {
    if (testInfo.project.name === 'mobile') await page.setViewportSize({ width: 320, height: 720 });
    await responsiveData(page, 'en', { activeCooking: false });
    await page.route('**/api/v1/users/me', (route) =>
      route.fulfill({
        json: {
          userId: '00000000-0000-4000-8000-000000000003',
          displayName: 'Alexandra',
          email: 'alex@example.test',
          isAdmin: false,
          version: 1,
          createdAt: '2026-10-05T00:00:00Z',
          households: [
            {
              householdId: '00000000-0000-4000-8000-000000000002',
              name: 'Our kitchen',
              role: 'owner'
            }
          ],
          assistance: { improve: false, draft: false, read: false, draw: true }
        }
      })
    );
    let request: Route | undefined;
    let requests = 0;
    await page.route(`**/api/v1/recipes/${recipeId}/image`, (route) => {
      if (route.request().method() !== 'POST') return route.fallback();
      request = route;
      requests++;
      // Leave the real request pending while its visual state is exercised.
    });
    await page.goto('/me/appearance');
    await page.getByRole('switch', { name: 'Animate Olli', exact: true }).click();
    await expect(page.getByRole('switch', { name: 'Animate Olli', exact: true })).not.toBeChecked();
    await page.goto(`/recipes/${recipeId}/edit`);
    await page.getByRole('button', { name: 'Create image', exact: true }).click();
    const frame = page.getByRole('group', { name: 'Photo', exact: true });
    const brush = frame.locator('.brush');
    await expect(brush).toBeVisible();
    await expect(frame.locator('.easel')).toBeVisible();
    await expect(frame.locator('.palette')).toBeVisible();
    await expect(frame.locator('.handle')).toHaveCount(2);
    await expect.poll(() => requests).toBe(1);
    await expect(frame.getByRole('button', { name: /animation/i })).toHaveCount(0);
    const pausedTransform = await brush.getAttribute('transform');
    // Exercise a real interval while the stream is still pending.
    await page.waitForTimeout(800);
    await expect(brush).toHaveAttribute('transform', pausedTransform!);
    await expect(frame.getByRole('status')).toHaveText('Creating image…');
    expect(requests).toBe(1);
    await frame.screenshot({ path: testInfo.outputPath('drawing-olli.png') });
    const fits = await frame.locator('.art, [role=status]').evaluateAll((nodes) =>
      nodes.every((node) => {
        const box = node.getBoundingClientRect();
        const frame = node.closest('.frame')!.getBoundingClientRect();
        return (
          box.left >= frame.left - 1 &&
          box.right <= frame.right + 1 &&
          box.top >= frame.top - 1 &&
          box.bottom <= frame.bottom + 1
        );
      })
    );
    expect(fits).toBe(true);

    await page.emulateMedia({ reducedMotion: 'reduce' });
    await expect(frame.getByRole('button', { name: 'Pause animation', exact: true })).toHaveCount(
      0
    );
    await expect(brush).toBeVisible();
    await request!.fulfill({
      contentType: 'text/event-stream',
      body: `data: ${JSON.stringify({ seconds: 18, finished: true, recipe: { imageId: 'image-2' } })}\n\n`
    });
    await expect(frame.locator('.generating-overlay')).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Create image', exact: true })).toBeEnabled();
    await expect(frame.locator('img')).toHaveAttribute('src', /image-2/);
  });
  test('drawing builds a picture across a longer phrase and retains it on the next cycle', async ({
    page
  }) => {
    await page.clock.install();
    await page.goto('/design');
    const frame = page.getByRole('group', { name: 'Photo, being drawn', exact: true });
    await frame.scrollIntoViewIfNeeded();
    await page.clock.runFor(2000);
    const olli = frame.locator('svg.olli');
    await expect(olli).toHaveAttribute('data-phase', 'painting');
    await page.clock.runFor(3500);
    await expect(olli).toHaveAttribute('data-phase', 'inspecting');
    await page.clock.runFor(12000);
    await expect(olli).toHaveAttribute('data-phase', 'painting');
    await page.clock.runFor(12000);
    await expect(frame.locator('.paint-detail')).toHaveAttribute('opacity', '1');
  });
});
