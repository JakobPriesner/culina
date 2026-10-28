import { expect, test, type Page } from '@playwright/test';
import { expectReflow, responsiveData } from './support/responsive';

/**
 * The settings area on a phone: the category row is one scrolling line (not wrapped rows above every page), the
 * administrator's group is named in it, and a row prints its label once with a note inset like the row above it.
 */
async function settingsAs(page: Page, width: number, isAdmin: boolean) {
  await page.setViewportSize({ width, height: 844 });
  await responsiveData(page, 'de', { activeCooking: false });
  await page.route('**/api/v1/users/me', (route) =>
    route.fulfill({
      json: {
        userId: '00000000-0000-4000-8000-000000000003',
        email: 'ada@example.test',
        displayName: 'Alexandra',
        isAdmin,
        createdAt: '2026-09-14T12:00:00Z',
        version: 1,
        households: [
          { householdId: '00000000-0000-4000-8000-000000000002', name: 'Küche', role: 'owner' }
        ],
        assistance: { improve: false, draft: false, read: false, draw: false }
      }
    })
  );
}

test.describe('settings @offline', () => {
  test.use({ serviceWorkers: 'block' });

  for (const width of [320, 390]) {
    test(`the categories are one row that keeps the current one in view (${width}px)`, async ({
      page
    }) => {
      await settingsAs(page, width, true);
      // The server page itself is not under test; its requests just fail.
      await page.route('**/api/v1/settings/**', (route) => route.fulfill({ status: 500 }));
      await page.goto('/me/server');

      const rail = page.locator('nav.rail');
      await expect(rail.getByRole('link', { name: 'Server' })).toHaveAttribute(
        'aria-current',
        'page'
      );
      await expect(rail.getByText('Verwaltung')).toBeVisible();

      const tops = await rail
        .getByRole('link')
        .evaluateAll((links) => links.map((link) => Math.round(link.getBoundingClientRect().top)));
      expect(new Set(tops).size).toBe(1);
      expect((await rail.boundingBox())!.height).toBeLessThan(72);

      // The current category was scrolled into the visible part of the row.
      const current = (await rail.getByRole('link', { name: 'Server' }).boundingBox())!;
      expect(current.x).toBeGreaterThanOrEqual(0);
      expect(current.x + current.width).toBeLessThanOrEqual(width + 1);

      // Every category is reachable by keyboard.
      await rail.getByRole('link').first().focus();
      const count = await rail.getByRole('link').count();
      for (let i = 1; i < count; i += 1) await page.keyboard.press('Tab');
      await expect(rail.getByRole('link').last()).toBeFocused();
      await expectReflow(page);
    });
  }

  for (const width of [320, 390, 768, 1024]) {
    test(`a diary row prints its label once and its note is inset like the row (${width}px)`, async ({
      page
    }) => {
      await settingsAs(page, width, false);
      await page.route('**/api/v1/users/me/settings/diary', (route) =>
        route.fulfill({
          json: {
            enabled: true,
            currentConsentVersion: 1,
            consentVersion: 1,
            kcalTarget: 2000,
            proteinTarget: 100,
            headline: 'eaten',
            version: 1
          }
        })
      );
      await page.goto('/me/diary');

      for (const name of ['Kilokalorien am Tag', 'Eiweiß am Tag (g)']) {
        await expect(page.getByRole('textbox', { name })).toBeVisible();
        await expect(page.getByText(name, { exact: true })).toHaveCount(1);
      }
      await expect(page.getByRole('switch')).toBeVisible();

      const note = await page.locator('p.honest').evaluate((el) => {
        return el.getBoundingClientRect().left + parseFloat(getComputedStyle(el).paddingLeft);
      });
      const label = await page
        .locator('.body > div')
        .first()
        .locator('.label')
        .first()
        .evaluate((el) => el.getBoundingClientRect().left);
      expect(Math.abs(note - label)).toBeLessThan(1);
      await expectReflow(page);
    });
  }
});
