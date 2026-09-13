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
 * The shopping list, the way it is used: one hand, one line at a time.
 *
 * Two things make this better than a notes app — amounts merge across recipes,
 * and the sections are the order a shop is walked — and nothing else is built.
 */
test.describe.configure({ mode: 'serial' });

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

    const field = page.getByRole('textbox', { name: /add|hinzufügen/i });
    const name = unique('Feta');

    await field.fill(`2 Packungen ${name}`);
    // A real key, not a synthetic event: a one-line add is only a one-line add
    // if Enter sends it, and reaching for the button after every item is the
    // data entry this screen exists to avoid.
    await field.press('Enter');

    // The field empties, which is the signal the line was taken: the next item
    // can be typed straight away.
    await expect(field).toHaveValue('');

    const row = page.getByRole('listitem').filter({ hasText: name });

    await expect(row).toBeVisible();
    // "2" alone would be a different shopping trip. The unit is not decoration.
    await expect(row).toContainText(/2\s*(Packungen|packs)/);

    await row.getByRole('button', { name: /remove|entfernen/i }).click();
    await expect(row).toHaveCount(0);
  });

  test('merges the same ingredient across two recipes', async () => {
    const butter = unique('Butter');

    // Two recipes that share an ingredient, at different amounts and in
    // different units: 200 g and 0.05 kg is 250 g of one thing, not two lines.
    const cake = await seedRecipe(page, {
      title: unique('Cake'),
      yieldAmount: 4,
      ingredients: [{ quantity: 200, unit: 'g', name: butter }]
    });

    const biscuits = await seedRecipe(page, {
      title: unique('Biscuits'),
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

  test('keeps what is already in the trolley, and clears it in one go', async () => {
    const name = unique('Salz');

    await page.goto('/shopping');

    const field = page.getByRole('textbox', { name: /add|hinzufügen/i });

    await field.fill(`1 Prise ${name}`);
    await field.press('Enter');
    await expect(field).toHaveValue('');

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

    const field = page.getByRole('textbox', { name: /add|hinzufügen/i });

    await field.fill(`1 kg ${name}`);
    await field.press('Enter');
    await expect(field).toHaveValue('');

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
