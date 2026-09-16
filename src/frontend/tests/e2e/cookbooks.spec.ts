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
 * A shelf you choose what goes on.
 *
 * The whole feature is one claim: a cookbook is a view of the collection rather
 * than a second one. This suite walks the path that proves it — make a shelf
 * from the recipe that suggested it, find it where the shelves live, search
 * inside it with the same box the collection uses, and then delete it and check
 * that every recipe survived.
 */
test.describe.configure({ mode: 'serial' });

test.describe('cookbooks', () => {
  test.skip(needsBackend, skipReason);

  let page: Page;
  let onTheShelf: string;
  let elsewhere: string;
  let name: string;

  test.beforeAll(async ({ browser }, testInfo) => {
    if (needsBackend) {
      return;
    }

    page = await browser.newPage();

    await signInWithHousehold(page, await accountFor(browser, testInfo));

    // Named uniquely, because these suites share one instance.
    onTheShelf = unique('Roast');
    elsewhere = unique('Gazpacho');
    name = unique('Sundays');

    await seedRecipe(page, { title: onTheShelf, yieldAmount: 4 });
    await seedRecipe(page, { title: elsewhere, yieldAmount: 4 });
  });

  test.afterAll(async () => {
    await page?.close();
  });

  test('a recipe is what suggests the shelf it belongs on', async () => {
    await page.goto('/');
    await page.getByRole('link', { name: new RegExp(onTheShelf) }).click();
    await expect(page.getByRole('heading', { level: 1, name: onTheShelf })).toBeVisible();

    await page.getByRole('button', { name: /add to cookbook|zu kochbuch/i }).click();

    // No cookbooks yet, so the sheet offers to make the first one rather than
    // showing an empty list and leaving it there.
    await page.getByRole('button', { name: /new cookbook|neues kochbuch/i }).click();
    await page.getByRole('textbox', { name: /^(name)$/i }).fill(name);
    await page.getByRole('button', { name: /make it|anlegen/i }).click();

    // Made and ticked in one move: making a cookbook is never the goal, putting
    // this recipe somewhere is.
    await expect(page.getByRole('checkbox', { name })).toBeChecked();
  });

  test('the shelf appears above the collection once there is one', async () => {
    await page.goto('/');

    await expect(
      page.getByRole('link', { name: new RegExp(`Open ${name}|${name} öffnen`) })
    ).toBeVisible();
  });

  test('it holds what was put on it, and nothing else', async () => {
    await page.goto('/cookbooks');
    await page.getByRole('link', { name: new RegExp(`Open ${name}|${name} öffnen`) }).click();

    await expect(page.getByRole('heading', { level: 1, name })).toBeVisible();
    await expect(page.getByRole('link', { name: new RegExp(onTheShelf) })).toBeVisible();
    await expect(page.getByRole('link', { name: new RegExp(elsewhere) })).toBeHidden();
  });

  test('searching inside it is the same search, narrowed', async () => {
    // This is the payoff of reading a cookbook through the recipe list: the box
    // works here without a line of code that knows about cookbooks.
    await page.getByRole('searchbox').fill('zzz-nothing-matches');
    await expect(page.getByText(/nothing here matched|hier passt nichts/i)).toBeVisible();

    await page
      .getByRole('button', { name: /clear|leeren|löschen/i })
      .first()
      .click();
    await expect(page.getByRole('link', { name: new RegExp(onTheShelf) })).toBeVisible();
  });

  test('deleting the shelf deletes no food', async () => {
    await page.getByRole('button', { name: /delete cookbook|kochbuch löschen/i }).click();

    await expect(page).toHaveURL(/\/cookbooks$/);
    await expect(
      page.getByText(/the recipes are still there|die rezepte sind noch da/i)
    ).toBeVisible();

    // The claim, checked rather than asserted in prose.
    await page.goto('/');
    await expect(page.getByRole('link', { name: new RegExp(onTheShelf) })).toBeVisible();
    await expect(page.getByRole('link', { name: new RegExp(elsewhere) })).toBeVisible();
  });
});

/**
 * A shelf that fills itself.
 *
 * The rules are stored and what matches them is not, so the only claim worth
 * walking through a browser is the one that would be a lie if anything were
 * cached: a recipe written afterwards is on it, with nothing run in between.
 */
test.describe('an automatic cookbook', () => {
  test.skip(needsBackend, skipReason);

  let page: Page;
  let tag: string;
  let name: string;

  test.beforeAll(async ({ browser }, testInfo) => {
    if (needsBackend) {
      return;
    }

    page = await browser.newPage();

    await signInWithHousehold(page, await accountFor(browser, testInfo));

    tag = unique('Kategorie');
    name = unique('Automatisch');

    await seedRecipe(page, { title: unique('Schon da'), tags: [tag] });
  });

  test.afterAll(async () => {
    await page?.close();
  });

  test('is already full the moment it is made', async () => {
    await page.goto('/cookbooks');
    await page.getByRole('button', { name: /new cookbook|neues kochbuch/i }).click();
    await page.getByRole('textbox', { name: /^(name)$/i }).fill(name);

    await page.getByRole('radio', { name: /fills itself|füllt sich selbst/i }).check();
    await page.getByRole('checkbox', { name: new RegExp(tag) }).check();
    await page.getByRole('button', { name: /make it|anlegen/i }).click();

    // A shelf somebody fills starts empty. One that fills itself never does.
    await expect(page.getByRole('heading', { level: 1, name })).toBeVisible();
    await expect(page.getByText(/^1 (recipes|Rezepte)$/)).toBeVisible();
  });

  test('takes a recipe written afterwards, with nothing run in between', async () => {
    const later = unique('Danach geschrieben');

    await seedRecipe(page, { title: later, tags: [tag] });
    await page.reload();

    await expect(page.getByRole('link', { name: new RegExp(later) })).toBeVisible();
  });

  test('offers its rules where a manual shelf offers recipes', async () => {
    await page.goto('/cookbooks');
    await page
      .getByRole('link', { name: new RegExp(`Open ${name}|${name} öffnen`) })
      .first()
      .click();

    await expect(
      page.getByRole('button', { name: /add recipes|rezepte hinzufügen/i })
    ).toBeHidden();
    await expect(page.getByRole('button', { name: /edit rules|regeln bearbeiten/i })).toBeVisible();
  });
});
