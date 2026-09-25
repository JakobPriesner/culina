import { expect, test, type Page } from '@playwright/test';

import {
  accountFor,
  needsBackend,
  seedRecipe,
  signInWithHousehold,
  skipReason,
  unique,
  writeHeaders
} from './support/culina';

/**
 * The shopping list, the way it is used: one hand, one line at a time.
 *
 * Two things make this better than a notes app — amounts merge across recipes,
 * and the sections are the order a shop is walked — and nothing else is built.
 */
test.describe.configure({ mode: 'serial' });

/**
 * The three fields a line is written in, the same ones the recipe editor uses.
 *
 * An amount, a unit and a name are three things, and asking for them apart is
 * what saves the app guessing where one ends and the next begins.
 */
const writing = (page: Page) => ({
  amount: page.getByRole('textbox', { name: /^(amount|menge)$/i }),
  unit: page.getByRole('combobox', { name: /^(unit|einheit)$/i }),
  name: page.getByRole('combobox', { name: /what to buy|was du brauchst/i })
});

/** Writes one line and sends it with the key that sends it. */
async function write(
  page: Page,
  line: { amount?: string; unit?: string; name: string }
): Promise<void> {
  const fields = writing(page);

  if (line.amount) {
    await fields.amount.fill(line.amount);
  }

  if (line.unit) {
    await fields.unit.fill(line.unit);
  }

  await fields.name.fill(line.name);

  // A real key, not a synthetic event: a one-line add is only a one-line add
  // if Enter sends it, and reaching for the button after every item is the
  // data entry this screen exists to avoid.
  await fields.name.press('Enter');

  // The fields empty, which is the signal the line was taken: the next item
  // can be typed straight away.
  await expect(fields.amount).toHaveValue('');
  await expect(fields.name).toHaveValue('');
}

test.describe('the shopping list', () => {
  test.skip(needsBackend, skipReason);

  let page: Page;

  test.beforeAll(async ({ browser }, testInfo) => {
    if (needsBackend) {
      return;
    }

    page = await browser.newPage();

    await signInWithHousehold(page, await accountFor(browser, testInfo));
  });

  test.afterAll(async () => {
    await page?.close();
  });

  test('takes a written line on Enter, with its unit intact', async () => {
    await page.goto('/shopping');

    // In German on purpose, and switched here rather than assumed: a new
    // account is in English, where every unit's word *is* its code and the
    // thing this test exists to prove cannot go wrong. "Packung" only means
    // `pack` in an app that is showing German.
    const switched = await page.request.put('/api/v1/users/me/settings', {
      headers: await writeHeaders(page),
      data: { locale: 'de', theme: 'warm-paper', mode: 'light', measurementSystem: 'metric' }
    });

    expect(switched.ok(), await switched.text()).toBe(true);

    await page.reload();

    const name = unique('Feta');

    // The word the unit list offers, not the code behind it: choosing "Packung"
    // has to mean `pack`, or the same unit becomes two as soon as somebody with
    // an English app opens the list.
    await write(page, { amount: '2', unit: 'Packung', name });

    const row = page.getByRole('listitem').filter({ hasText: name });

    await expect(row).toBeVisible();
    // "2" alone would be a different shopping trip. The unit is not decoration.
    await expect(row).toContainText(/2\s*(Packungen|packs)/);

    await row.getByRole('button', { name: /remove|entfernen/i }).click();
    await expect(row).toHaveCount(0);
  });

  test('merges the same ingredient across two recipes', async () => {
    const butter = unique('Butter');
    const cakeTitle = unique('Cake');
    const biscuitsTitle = unique('Biscuits');

    // Two recipes that share an ingredient, at different amounts and in
    // different units: 200 g and 0.05 kg is 250 g of one thing, not two lines.
    const cake = await seedRecipe(page, {
      title: cakeTitle,
      yieldAmount: 4,
      ingredients: [{ quantity: 200, unit: 'g', name: butter }]
    });

    const biscuits = await seedRecipe(page, {
      title: biscuitsTitle,
      yieldAmount: 4,
      ingredients: [{ quantity: 0.05, unit: 'kg', name: butter }]
    });

    for (const recipeId of [cake, biscuits]) {
      await page.goto(`/recipes/${recipeId}`);
      await page.getByRole('button', { name: /shopping list|einkaufsliste/i }).click();
      await expect(page.getByText(/added to|hinzugefügt/i)).toBeVisible();
    }

    await page.goto('/shopping');

    const row = page.getByRole('listitem').filter({ hasText: butter });

    // One line, adding up to what it actually adds up to. Three recipes and
    // three lines of butter is how a list stops being worth carrying.
    await expect(row).toHaveCount(1);
    await expect(row).toContainText(/250\s*g/);

    // The merged line stays compact until its explanation is wanted, then
    // every contribution leads back to the recipe that asked for it.
    await row.getByRole('button', { name: /2 recipes|2 Rezepte/i }).click();
    await expect(row.getByRole('link', { name: cakeTitle })).toBeVisible();
    await expect(row.getByRole('link', { name: biscuitsTitle })).toBeVisible();

    await row.getByRole('button', { name: /remove|entfernen/i }).click();
    await expect(row).toHaveCount(0);
  });

  test('adds a recipe at the servings on screen, not the ones it was written for', async () => {
    const orzo = unique('Orzo');
    const recipeId = await seedRecipe(page, {
      title: unique('Scaled to six'),
      yieldAmount: 2,
      ingredients: [{ quantity: 200, unit: 'g', name: orzo }]
    });

    // Scaled to six on screen. Getting the amounts for two is the kind of quiet
    // wrongness nobody notices until they are short of butter.
    await page.goto(`/recipes/${recipeId}?yield=6`);
    await page.getByRole('button', { name: /shopping list|einkaufsliste/i }).click();
    await expect(page.getByText(/added to|hinzugefügt/i)).toBeVisible();

    await page.goto('/shopping');

    const row = page.getByRole('listitem').filter({ hasText: orzo });

    await expect(row).toContainText(/600\s*g/);

    await row.getByRole('button', { name: /remove|entfernen/i }).click();
  });

  /*
   * The other half of how a list fills up. Filling next week's list means
   * naming four or five recipes in a row, and doing that from the recipe pages
   * is four round trips through a list and back — so the question gets asked
   * here, where the answer goes.
   */
  test('takes whole recipes from the list itself, several in one opening', async () => {
    const rice = unique('Reis');
    const basil = unique('Basilikum');
    const risotto = unique('Risotto');
    const pesto = unique('Pesto');

    await seedRecipe(page, {
      title: risotto,
      yieldAmount: 4,
      ingredients: [{ quantity: 300, unit: 'g', name: rice }]
    });

    await seedRecipe(page, {
      title: pesto,
      yieldAmount: 4,
      ingredients: [{ quantity: 50, unit: 'g', name: basil }]
    });

    await page.goto('/shopping');

    // The one above the list. An empty list offers the same invitation again
    // in its empty state, which is the design system's rule about dead ends
    // rather than an accident.
    await page
      .getByRole('button', { name: /add a recipe|rezept hinzufügen/i })
      .first()
      .click();

    const picker = page.getByRole('dialog');

    await expect(picker).toBeVisible();

    const search = picker.getByRole('searchbox');

    await search.fill(risotto);
    await picker.getByRole('button', { name: new RegExp(risotto) }).click();

    // Still up, and saying what it did — the week's list is four or five
    // recipes and reopening the sheet for each was the slow part.
    await expect(picker).toBeVisible();
    await expect(picker.getByRole('button', { name: new RegExp(risotto) })).toContainText(
      /added|hinzugefügt/i
    );

    await search.fill(pesto);
    await picker.getByRole('button', { name: new RegExp(pesto) }).click();

    await picker.getByRole('button', { name: /^(done|fertig)$/i }).click();
    await expect(picker).toBeHidden();

    const riceRow = page.getByRole('listitem').filter({ hasText: rice });
    const basilRow = page.getByRole('listitem').filter({ hasText: basil });

    // At the yield each recipe is written for, which is what the picker row
    // said it was.
    await expect(riceRow).toContainText(/300\s*g/);
    await expect(basilRow).toContainText(/50\s*g/);

    for (const row of [riceRow, basilRow]) {
      await row.getByRole('button', { name: /remove|entfernen/i }).click();
      await expect(row).toHaveCount(0);
    }
  });

  test('keeps what is already in the trolley, and clears it in one go', async () => {
    const name = unique('Salz');

    await page.goto('/shopping');

    await write(page, { amount: '1', unit: 'Prise', name });

    const row = page.getByRole('listitem').filter({ hasText: name });

    await expect(row).toBeVisible();

    await row.getByRole('checkbox').check();

    // Ticked off, and still on screen: a line that vanishes when it is found is
    // a line nobody can check they actually picked up.
    await expect(row).toBeVisible();
    await expect(row.getByRole('checkbox')).toBeChecked();

    // One bulk action, because after a shop removing a dozen ticked lines one
    // at a time is the tedium this exists to avoid.
    await page.getByRole('button', { name: /clear|gekauftes entfernen/i }).click();

    await expect(row).toHaveCount(0);
  });

  test('keeps its rows clear of the bars pinned to the bottom', async () => {
    const name = unique('Mehl');

    await page.goto('/shopping');

    await write(page, { amount: '1', unit: 'kg', name });

    const row = page.getByRole('listitem').filter({ hasText: name });

    await expect(row).toBeVisible();

    // All the way down, where the shell has parked the cooking bar and, on a
    // phone, the navigation. A row either of them covers cannot be ticked off,
    // and on a phone that is most of the screen.
    await page.evaluate(() => window.scrollTo(0, document.body.scrollHeight));

    const last = page.getByRole('listitem').last();
    const box = (await last.boundingBox())!;

    const covered = await page.evaluate(
      ([y, height]) => {
        const at = document.elementFromPoint(20, y! + height! / 2);

        return at ? !at.closest('main') : true;
      },
      [box.y, box.height]
    );

    expect(covered, 'the last row is behind a bar').toBe(false);

    await row.getByRole('button', { name: /remove|entfernen/i }).click();
  });
});
