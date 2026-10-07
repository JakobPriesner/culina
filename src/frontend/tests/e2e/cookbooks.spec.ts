import { expect, test, type Page } from '@playwright/test';

import {
  accountFor,
  needsBackend,
  seedRecipe,
  signInWithHousehold,
  skipReason,
  unique
} from './support/culina';

/** A cookbook is a view of the collection: made from a recipe, searched like it, and deleting it spares every recipe. */
test.describe.configure({ mode: 'serial' });

test.describe('cookbooks', () => {
  test.skip(needsBackend, skipReason);

  let page: Page;
  let onTheShelf: string;
  let onTheShelfId: string;
  let elsewhere: string;
  let name: string;

  test.beforeAll(async ({ browser }, testInfo) => {
    if (needsBackend) {
      return;
    }

    page = await browser.newPage();

    await signInWithHousehold(page, await accountFor(browser, testInfo));

    onTheShelf = unique('Roast');
    elsewhere = unique('Gazpacho');
    name = unique('Sundays');

    onTheShelfId = await seedRecipe(page, { title: onTheShelf, yieldAmount: 4 });
    await seedRecipe(page, { title: elsewhere, yieldAmount: 4 });
  });

  test.afterAll(async () => {
    await page?.close();
  });

  test('a recipe is what suggests the shelf it belongs on', async () => {
    // Straight to the recipe page: the library may show it as a shortlist heading, with no link to click.
    await page.goto(`/recipes/${onTheShelfId}`);
    await expect(page.getByRole('heading', { level: 1, name: onTheShelf })).toBeVisible();

    await page.getByRole('button', { name: /more actions|weitere aktionen/i }).click();
    await page.getByRole('button', { name: /add to cookbook|kochbuch/i }).click();

    await page.getByRole('button', { name: /new cookbook|neues kochbuch/i }).click();
    await page.getByRole('textbox', { name: /^(name)$/i }).fill(name);

    // The tick is optimistic; wait for the membership to reach the server before leaving.
    const membershipWritten = page.waitForResponse(
      (response) =>
        response.request().method() === 'PUT' &&
        /\/cookbooks\/[^/]+\/recipes\/[^/]+$/.test(new URL(response.url()).pathname) &&
        response.ok()
    );

    await page.getByRole('button', { name: /create cookbook|anlegen/i }).click();

    await expect(page.getByRole('checkbox', { name })).toBeChecked();
    await membershipWritten;
  });

  test('it holds what was put on it, and nothing else', async () => {
    await page.goto('/cookbooks');
    await page.getByRole('link', { name: new RegExp(`Open ${name}|${name} öffnen`) }).click();

    await expect(page.getByRole('heading', { level: 1, name })).toBeVisible();
    await expect(page.getByRole('link', { name: new RegExp(onTheShelf) })).toBeVisible();
    await expect(page.getByRole('link', { name: new RegExp(elsewhere) })).toBeHidden();
  });

  test('searching inside it is the same search, narrowed', async () => {
    await page.getByRole('searchbox').fill('zzz-nothing-matches');
    await expect(page.getByText(/nothing here matched|keine treffer/i)).toBeVisible();

    // Whole name: "löschen" also matches the delete-cookbook button in German.
    await page.getByRole('button', { name: /^(clear search|suche zurücksetzen)$/i }).click();
    await expect(page.getByRole('link', { name: new RegExp(onTheShelf) })).toBeVisible();
  });

  test('deleting the shelf deletes no food', async () => {
    await page
      .getByRole('button', { name: /more cookbook actions|weitere kochbuchaktionen/i })
      .click();
    await page.getByRole('button', { name: /delete this cookbook|kochbuch löschen/i }).click();

    const question = page.getByRole('dialog', { name: new RegExp(name) });

    await expect(question).toContainText(
      /recipes stay in your library|rezepte bleiben in deiner sammlung/i
    );
    await question.getByRole('button', { name: /delete this cookbook|kochbuch löschen/i }).click();

    await expect(page).toHaveURL(/\/cookbooks$/);
    await expect(
      page.getByText(/the recipes are still there|deine rezepte bleiben erhalten/i)
    ).toBeVisible();

    // First match: a shortlisted recipe is also a heading there with a "Stop suggesting" button.
    await page.goto('/');
    await expect(page.getByText(onTheShelf).first()).toBeVisible();
    await expect(page.getByText(elsewhere).first()).toBeVisible();
  });
});

/** An automatic cookbook: a recipe written afterwards is on it with nothing run in between. */
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

    await page
      .getByRole('radio', { name: /add them automatically|automatisch nach regeln/i })
      .check();
    const sheet = page.getByRole('dialog');
    const find = sheet.getByRole('searchbox', { name: /find a tag|schlagwort suchen/i });

    if (await find.isVisible()) {
      await find.fill(tag);
    }

    await sheet.getByRole('button', { name: new RegExp(tag) }).click();
    await expect(sheet.getByRole('button', { name: new RegExp(tag) })).toHaveAttribute(
      'aria-pressed',
      'true'
    );
    // Anchored: "anlegen" is also the end of "Erstes Kochbuch anlegen".
    await page.getByRole('button', { name: /^(create cookbook|anlegen)$/i }).click();

    await page
      .getByRole('link', { name: new RegExp(`Open ${name}|${name} öffnen`) })
      .first()
      .click();

    await expect(page.getByRole('heading', { level: 1, name })).toBeVisible();
    await expect(page.getByText(/^1 (recipe|Rezept)$/)).toBeVisible();
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
      page.getByRole('button', { name: /add to cookbook|zu einem kochbuch hinzufügen/i })
    ).toBeHidden();
    await expect(
      page.getByRole('button', { name: /change the rules|regeln bearbeiten/i })
    ).toBeVisible();
  });
});
