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

/** The shopping list as used one-handed: amounts merge across recipes and sections follow the shop's walking order. */
test.describe.configure({ mode: 'serial' });

/** Amount, unit and name are separate fields, as in the recipe editor. */
const writing = (page: Page) => ({
  amount: page.getByRole('textbox', { name: /^(amount|menge)$/i }),
  unit: page.getByRole('combobox', { name: /^(unit|einheit)$/i }),
  name: page.getByRole('combobox', { name: /what to buy|was du brauchst/i })
});

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

  // A real Enter key: a one-line add must send on Enter.
  await fields.name.press('Enter');

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

    // German on purpose: in a new English account every unit's word is its code, so this test could not fail.
    const switched = await page.request.put('/api/v1/users/me/settings', {
      headers: await writeHeaders(page),
      data: { locale: 'de', theme: 'warm-paper', mode: 'light', measurementSystem: 'metric' }
    });

    expect(switched.ok(), await switched.text()).toBe(true);

    await page.reload();

    const name = unique('Feta');

    // Choosing the displayed word "Packung" must store `pack`, or one unit becomes two.
    await write(page, { amount: '2', unit: 'Packung', name });

    const row = page.getByRole('listitem').filter({ hasText: name });

    await expect(row).toBeVisible();
    await expect(row).toContainText(/2\s*(Packungen|packs)/);

    await row.getByRole('button', { name: /remove|entfernen/i }).click();
    await expect(row).toHaveCount(0);
  });

  test('merges the same ingredient across two recipes', async () => {
    const butter = unique('Butter');
    const cakeTitle = unique('Cake');
    const biscuitsTitle = unique('Biscuits');

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

    await expect(row).toHaveCount(1);
    await expect(row).toContainText(/250\s*g/);

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

    await page.goto(`/recipes/${recipeId}?yield=6`);
    await page.getByRole('button', { name: /shopping list|einkaufsliste/i }).click();
    await expect(page.getByText(/added to|hinzugefügt/i)).toBeVisible();

    await page.goto('/shopping');

    const row = page.getByRole('listitem').filter({ hasText: orzo });

    await expect(row).toContainText(/600\s*g/);

    await row.getByRole('button', { name: /remove|entfernen/i }).click();
  });

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

    await page
      .getByRole('button', { name: /add a recipe|rezept hinzufügen/i })
      .first()
      .click();

    const picker = page.getByRole('dialog');

    await expect(picker).toBeVisible();

    const search = picker.getByRole('searchbox');

    await search.fill(risotto);
    await picker.getByRole('button', { name: new RegExp(risotto) }).click();

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

    await expect(row).toBeVisible();
    await expect(row.getByRole('checkbox')).toBeChecked();

    await page.getByRole('button', { name: /clear|gekauftes entfernen/i }).click();

    await expect(row).toHaveCount(0);
  });

  test('remembers a line moved to another section for the next time it is added', async () => {
    const name = unique('Erbsen');

    await page.goto('/shopping');

    const frozen = page
      .locator('section')
      .filter({ has: page.getByRole('heading', { name: /frozen|tiefkühl/i }) });
    const row = page.getByRole('listitem').filter({ hasText: name });

    await write(page, { name });
    await expect(row).toBeVisible();
    await expect(frozen.getByRole('listitem').filter({ hasText: name })).toHaveCount(0);

    await row.getByRole('button', { name: /another section|anderen bereich/i }).click();
    await page.getByRole('button', { name: /^(frozen|tiefkühlprodukte)$/i }).click();

    await expect(frozen.getByRole('listitem').filter({ hasText: name })).toBeVisible();

    await row.getByRole('button', { name: /remove|entfernen/i }).click();
    await expect(row).toHaveCount(0);

    await write(page, { name: name.toLowerCase() });

    await expect(
      frozen.getByRole('listitem').filter({ hasText: new RegExp(name, 'i') })
    ).toBeVisible();

    await row.getByRole('button', { name: /remove|entfernen/i }).click();
  });

  test('keeps its rows clear of the bars pinned to the bottom', async () => {
    const name = unique('Mehl');

    await page.goto('/shopping');

    await write(page, { amount: '1', unit: 'kg', name });

    const row = page.getByRole('listitem').filter({ hasText: name });

    await expect(row).toBeVisible();

    // Scroll to the bottom, where the cooking bar (and on phones the navigation) can cover a row.
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
