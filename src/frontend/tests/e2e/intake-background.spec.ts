import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';
import { expectReflow, responsiveData } from './support/responsive';

const id = '00000000-0000-4000-8000-000000000055';
const recipeId = '00000000-0000-4000-8000-000000000001';
const householdId = '00000000-0000-4000-8000-000000000002';
const draft = {
  draftId: id,
  title: 'Beans with herbs',
  yieldAmount: 2,
  groups: [{ ingredients: [{ name: 'beans', quantity: 120, unit: 'g' }] }],
  steps: [{ text: 'Fry the beans.' }],
  tags: []
};

test.describe('background intake @offline', () => {
  test.use({ serviceWorkers: 'block' });
  test('restores progress across navigation and reload, then reviews the saved recipe', async ({
    page
  }, testInfo) => {
    await responsiveData(page, 'en', { activeCooking: false });
    let stage = 'thinking';
    let reviewed = false;
    const job = () => ({
      id,
      householdId,
      stage,
      createdAt: '2026-10-05T00:00:00Z',
      photoCount: 0,
      material: '120 g beans\nFry the beans.',
      sourceUrl: 'https://example.com/recipe',
      draft: stage === 'thinking' ? null : draft,
      recipeId: stage === 'ready' ? recipeId : null
    });
    await page.route('**/api/v1/recipe-intakes', (route) =>
      route.fulfill({ json: reviewed ? [] : [job()] })
    );
    await page.route(`**/api/v1/recipe-intakes/${id}`, (route) => route.fulfill({ json: job() }));
    await page.goto(`/recipes/imports/${id}`);
    await expect(
      page.getByRole('status').filter({ hasText: 'Working out the recipe' })
    ).toBeVisible();
    await expect(
      page.getByText('You can leave this page or close the app.', { exact: false })
    ).toBeVisible();
    await page.getByRole('button', { name: 'Pause animation', exact: true }).click();
    await expect(page.getByRole('button', { name: 'Play animation', exact: true })).toBeVisible();
    await page.goto('/recipes');
    await expect(page.getByRole('link', { name: 'Olli is working', exact: true })).toBeVisible();
    stage = 'writing';
    await page.goto(`/recipes/imports/${id}`);
    await expect(page.getByRole('status').filter({ hasText: 'Writing your recipe' })).toBeVisible();
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await page.reload();
    await expect(page.getByText('Fry the beans.', { exact: true })).toBeVisible();
    await expectReflow(page);
    await page.screenshot({ path: testInfo.outputPath('olli-writing.png'), fullPage: true });
    stage = 'ready';
    await page.reload();
    await expect(page.getByText('Already saved in your kitchen', { exact: true })).toBeVisible();
    await expect(page.getByRole('link', { name: 'Review and edit', exact: true })).toHaveAttribute(
      'href',
      `/recipes/${recipeId}/edit`
    );
    await expectReflow(page);
    expect((await new AxeBuilder({ page }).include('.intake-page').analyze()).violations).toEqual(
      []
    );
    await page.screenshot({ path: testInfo.outputPath('olli-ready.png'), fullPage: true });
    await page.route(`**/api/v1/recipe-intakes/${id}/reviewed`, (route) => {
      reviewed = true;
      return route.fulfill({ status: 204 });
    });
    await page.getByRole('button', { name: 'Finish review', exact: true }).click();
    await expect(page).toHaveURL(`/recipes/${recipeId}`);
    expect(reviewed).toBe(true);
  });
  test('hands a slow source link to the server before opening progress', async ({ page }) => {
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
          households: [{ householdId, name: 'Our kitchen', role: 'owner' }],
          assistance: { improve: false, draft: false, read: true, draw: false }
        }
      })
    );
    // Keep the quick preview request waiting. The durable start must not need it.
    await page.route('**/api/v1/recipe-imports', () => {});
    let accepted: Record<string, unknown> | null = null;
    let submitted = '';
    await page.route('**/api/v1/recipe-intakes**', async (route) => {
      const request = route.request();
      const url = new URL(request.url());
      if (request.method() === 'POST' && url.pathname === '/api/v1/recipe-intakes') {
        submitted = request.postData() ?? '';
        accepted = {
          id: url.searchParams.get('id'),
          householdId,
          stage: 'reading',
          createdAt: '2026-10-05T00:00:00Z',
          photoCount: 0,
          sourceUrl: 'https://example.com/beans',
          material: '120 g beans #ad'
        };
        return route.fulfill({ status: 202, json: accepted });
      }
      return route.fulfill({
        json: url.pathname === '/api/v1/recipe-intakes' ? (accepted ? [accepted] : []) : accepted
      });
    });
    await page.goto(
      '/recipes/new?url=https%3A%2F%2Fexample.com%2Fbeans&text=120%20g%20beans%20%23ad'
    );
    await page.getByRole('button', { name: 'Read with AI', exact: true }).click();
    await expect(page).toHaveURL(/\/recipes\/imports\/[a-f0-9-]+$/);
    expect(submitted).toContain('name="fetchSource"\r\n\r\ntrue');
    expect(submitted).toContain('120 g beans #ad');
    expect(submitted).toContain('https://example.com/beans');
    await expect(page.getByRole('status').filter({ hasText: 'Reading the source' })).toBeVisible();
  });
  test('shows the real work props and stops repeating animation when paused', async ({
    page
  }, testInfo) => {
    await responsiveData(page, 'en', { activeCooking: false });
    let job = {
      id,
      householdId,
      stage: 'reading',
      createdAt: '2026-10-05T00:00:00Z',
      photoCount: 0,
      material: 'Beans',
      draft: null
    };
    await page.route('**/api/v1/recipe-intakes', (route) => route.fulfill({ json: [job] }));
    await page.route(`**/api/v1/recipe-intakes/${id}`, (route) => route.fulfill({ json: job }));
    await page.goto(`/recipes/imports/${id}`);
    await expect(page.locator('.earbuds')).toBeVisible();
    await expect(page.locator('.phone')).toBeVisible();
    await page.getByRole('button', { name: 'Pause animation', exact: true }).click();
    await page.screenshot({ path: testInfo.outputPath('olli-watching.png'), fullPage: true });
    job = { ...job, stage: 'writing' };
    await page.reload();
    await expect(page.locator('.pencil')).toBeVisible();
    // Inspect a frame during the first stroke, with ordinary motion enabled.
    await page.waitForTimeout(700);
    await page
      .locator('svg.olli')
      .screenshot({ path: testInfo.outputPath('olli-writing-motion.png') });
    await page.getByRole('button', { name: 'Pause animation', exact: true }).click();
    const pen = page.locator('.pencil').locator('..');
    const pausedTransform = await pen.getAttribute('transform');
    await page.waitForTimeout(800);
    await expect(pen).toHaveAttribute('transform', pausedTransform!);
  });
});
