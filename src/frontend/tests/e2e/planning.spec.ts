import { expect, test, type Page } from '@playwright/test';

import {
  accountFor,
  needsBackend,
  seedRecipe,
  signInWithHousehold,
  skipReason,
  unique
} from './support/culina';

/**
 * A week of what this household means to cook.
 *
 * Seven days and deliberately not a calendar: a week is the unit people plan
 * in, because they shop at the weekend for the week that follows. Its whole
 * payoff is the last step — the plan writes the shopping list, through exactly
 * the same merge a single recipe goes through.
 */
test.describe.configure({ mode: 'serial' });

test.describe('planning a week', () => {
  test.skip(needsBackend, skipReason);

  let page: Page;
  let title: string;
  let ingredient: string;

  test.beforeAll(async ({ browser }, testInfo) => {
    if (needsBackend) {
      return;
    }

    page = await browser.newPage();

    await signInWithHousehold(page, await accountFor(browser, testInfo));

    title = unique('Planned');
    // Named uniquely, because these suites share one instance: asserting on
    // "200 g" would be asserting on whatever the browser next door is doing.
    ingredient = unique('Butter');

    await seedRecipe(page, {
      title,
      yieldAmount: 2,
      ingredients: [{ quantity: 200, unit: 'g', name: ingredient }],
      steps: ['Melt {0}.']
    });
  });

  test.afterAll(async () => {
    await page?.close();
  });

  test('puts a recipe on a day, and the week writes the shopping list', async () => {
    await page.goto('/');
    await page.getByRole('link', { name: /plan the week|woche planen/i }).click();

    await expect(page).toHaveURL(/\/plan$/);

    // Seven days, planned or not: a week with holes in it is a week the screen
    // has to fill in itself.
    await expect(
      page.getByRole('listitem').filter({ has: page.getByRole('heading', { level: 2 }) })
    ).toHaveCount(7);

    await page
      .getByRole('button', { name: /^\+ (add|hinzufügen)$/i })
      .first()
      .click();

    const sheet = page.getByRole('dialog');

    await expect(sheet).toBeVisible();
    await sheet.getByRole('searchbox').fill(title);
    await sheet.getByRole('button', { name: title }).click();

    await expect(sheet).toBeHidden();
    await expect(page.getByRole('link', { name: title })).toBeVisible();

    // The payoff. It goes through the same call a single recipe does, so the
    // merging that makes a list worth having cannot be subtly different here.
    await page.getByRole('button', { name: /add the week|woche auf die/i }).click();
    await expect(page.getByRole('status').getByText(/shopping list|einkaufsliste/i)).toBeVisible();

    await page.goto('/shopping');
    await expect(page.getByText(ingredient)).toBeVisible();
  });

  test('takes a meal off the plan again', async () => {
    await page.goto('/plan');

    await expect(page.getByRole('link', { name: title })).toBeVisible();

    await page.getByRole('button', { name: new RegExp(`${title}`, 'i') }).click();

    // That this meal is gone, not that the week is. These suites share one
    // instance, so an earlier run's Thursday is still somebody's Thursday.
    await expect(page.getByRole('link', { name: title })).toBeHidden();
  });
});
