import { expect, test, type Page } from '@playwright/test';

import {
  accountFor,
  needsBackend,
  opens,
  signInWithHousehold,
  skipReason,
  unique
} from './support/culina';

/** Recipe writing: a title alone makes a recipe; an ingredient is amount, unit and name. */
/** The recipe's ingredients, excluding the hint ("200 g flour"), which an amount assertion would also match. */
const ingredientsOf = (page: Page) =>
  page.getByRole('region', { name: /^(ingredients|zutaten)$/i }).getByRole('list');

const newIngredient = (page: Page) =>
  page.getByRole('group', { name: /new ingredient|neue zutat/i });

const png = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==',
  'base64'
);

interface Written {
  readonly amount?: string;
  readonly unit?: string;
  readonly name: string;
  readonly note?: string;
}

/** Writes one ingredient; the name goes last and carries Enter, since it is what makes the row an ingredient. */
async function write(page: Page, { amount = '', unit = '', name, note = '' }: Written) {
  const row = newIngredient(page);

  // Padded: the label wraps its text in a span beside the input, so an anchored pattern never matches.
  await row.getByLabel(/^\s*(amount|menge)\s*$/i).fill(amount);
  await row.getByRole('combobox', { name: /^(unit|einheit)$/i }).fill(unit);
  await row.getByLabel(/^\s*(note|hinweis)/i).fill(note);

  // Not end-anchored: once a suggestion is arrowed to, the accessible name gains the option ("Ingredient Potatoes").
  const field = row.getByRole('combobox', { name: /^\s*(ingredient|zutat)\b/i });

  await field.fill(name);
  await field.press('Enter');
}

test.describe.configure({ mode: 'serial' });

test.describe('writing a recipe', () => {
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

  test('exists as soon as it has a title, and fills in from there', async () => {
    const title = unique('Written');

    await page.goto('/recipes/new');
    await page.getByLabel(/^\s*(title|titel)\s*$/i).fill(title);
    await page.getByRole('button', { name: /create recipe|rezept erstellen/i }).click();

    await expect(page).toHaveURL(/\/recipes\/[0-9a-f-]+\/edit/);

    await write(page, { amount: '200', unit: 'g', name: 'Butter' });

    await expect(ingredientsOf(page).getByText('Butter')).toBeVisible();
    await expect(ingredientsOf(page).getByText(/200\s*g/)).toBeVisible();

    await write(page, { amount: '1,5', unit: 'kg', name: 'Mehl' });

    await expect(page.getByText(/1[.,]5\s*kg/)).toBeVisible();

    await write(page, { amount: '2', name: 'Zwiebeln', note: 'fein gehackt' });

    await expect(page.getByText('Zwiebeln')).toBeVisible();
    await expect(page.getByText(/fein gehackt/)).toBeVisible();

    await expect(newIngredient(page).getByLabel(/^\s*(amount|menge)\s*$/i)).toHaveValue('');

    await expect(page.getByText(/^(saved|gespeichert)$/i)).toBeVisible();

    await page.goto(`/recipes/${new URL(page.url()).pathname.split('/')[2]}`);

    await expect(page.getByRole('heading', { level: 1 })).toHaveText(title);
    await expect(page.getByRole('region', { name: /ingredients|zutaten/i })).toContainText('Mehl');
  });

  test('mentions an ingredient in a step, and the step carries its amount', async () => {
    const title = unique('Mentioned');

    await page.goto('/recipes/new');
    await page.getByLabel(/^\s*(title|titel)\s*$/i).fill(title);
    await page.getByRole('button', { name: /create recipe|rezept erstellen/i }).click();
    await expect(page).toHaveURL(/\/edit/);

    await write(page, { amount: '200', unit: 'g', name: 'Butter' });
    await expect(ingredientsOf(page).getByText(/200\s*g/)).toBeVisible();

    // Saved first: a line the server has not seen has no id for the step to point at.
    await expect(page.getByText(/^(saved|gespeichert)$/i)).toBeVisible();

    await page.getByRole('button', { name: /add a step|schritt hinzufügen/i }).click();

    const step = page.getByRole('combobox', { name: /step 1|schritt 1/i });

    await step.fill('Schmilz @But');
    await expect(page.getByRole('option', { name: /Butter/ })).toBeVisible();
    await step.press('Enter');
    await expect(step).toHaveValue('Schmilz @Butter ');

    await step.pressSequentially('in der Pfanne.');
    await expect(page.getByText(/^(saved|gespeichert)$/i)).toBeVisible();

    await page.goto(`/recipes/${new URL(page.url()).pathname.split('/')[2]}`);

    const method = page.getByRole('region', { name: /^(steps|zubereitung)$/i });

    await expect(method).toContainText(/200\s*g\s*Butter/);

    await page.getByRole('button', { name: /^(one more|eine mehr)$/i }).click();

    await expect(method).toContainText(/250\s*g\s*Butter/);
  });

  test('keeps saving after the photo is added or removed', async () => {
    const title = unique('Photographed');

    await page.goto('/recipes/new');
    await page.getByLabel(/^\s*(title|titel)\s*$/i).fill(title);
    await page.getByRole('button', { name: /create recipe|rezept erstellen/i }).click();
    await expect(page).toHaveURL(/\/edit/);

    await write(page, { amount: '200', unit: 'g', name: 'Butter' });
    await expect(page.getByText(/^(saved|gespeichert)$/i)).toBeVisible();

    // Saved by its own endpoint yet bumps the version; later saves used to be refused as a conflict.
    await page
      .getByLabel(/choose a photo|foto auswählen/i)
      .setInputFiles({ name: 'dish.png', mimeType: 'image/png', buffer: png });
    await expect(
      page.getByRole('button', { name: /remove the photo|foto entfernen/i })
    ).toBeVisible();

    await page.getByRole('button', { name: /add a step|schritt hinzufügen/i }).click();

    const step = page.getByRole('combobox', { name: /step 1|schritt 1/i });

    await step.fill('Schmilz @But');
    await step.press('Enter');
    await step.pressSequentially('in der Pfanne.');

    await expect(page.getByText(/^(saved|gespeichert)$/i)).toBeVisible();

    await page.getByRole('button', { name: /remove the photo|foto entfernen/i }).click();
    await expect(
      page.getByRole('button', { name: /choose a photo|foto auswählen/i })
    ).toBeVisible();

    await step.pressSequentially(' Dann servieren.');

    await expect(page.getByText(/^(saved|gespeichert)$/i)).toBeVisible();
    await expect(
      page.getByText(/someone changed this recipe|jemand hat dieses rezept geändert/i)
    ).toHaveCount(0);
  });

  test('measures in a unit this kitchen invented, and scales it', async () => {
    const title = unique('Schuss');

    await page.goto('/recipes/new');
    await page.getByLabel(/^\s*(title|titel)\s*$/i).fill(title);
    await page.getByRole('button', { name: /create recipe|rezept erstellen/i }).click();
    await expect(page).toHaveURL(/\/edit/);

    // Wait for the write, not "Saved": that word is already on screen from the previous save.
    const saved = () => page.waitForResponse((one) => one.request().method() === 'PUT' && one.ok());

    await Promise.all([saved(), write(page, { amount: '1', unit: 'Schuss', name: 'Milch' })]);

    const unit = newIngredient(page).getByRole('combobox', { name: /^(unit|einheit)$/i });

    await unit.fill('Schu');
    await expect(
      page
        .getByRole('listbox', { name: /unit suggestions|vorschläge für einheiten/i })
        .getByRole('option', { name: 'Schuss' })
    ).toBeVisible();

    await Promise.all([saved(), write(page, { amount: '2', unit: 'Schuss', name: 'Sahne' })]);

    await page.goto(`/recipes/${new URL(page.url()).pathname.split('/')[2]}`);

    const list = page.getByRole('region', { name: /ingredients|zutaten/i });

    await expect(list).toContainText(/1\s*Schuss/);
    await expect(list).toContainText(/2\s*Schuss/);

    await page.getByRole('button', { name: /^(one more|eine mehr)$/i }).click();

    await expect(list).toContainText(/2[–-]3\s*Schuss/);
  });

  test('suggests an ingredient, seeded first and then in this kitchen’s own words', async () => {
    const title = unique('Suggested');
    const invented = unique('Herbbutter').replace(/\s/g, '');

    await page.goto('/recipes/new');
    await page.getByLabel(/^\s*(title|titel)\s*$/i).fill(title);
    await page.getByRole('button', { name: /create recipe|rezept erstellen/i }).click();
    await expect(page).toHaveURL(/\/edit/);

    const row = newIngredient(page);
    // Not end-anchored: the accessible name gains the highlighted option once arrowed to.
    const field = row.getByRole('combobox', { name: /^\s*(ingredient|zutat)\b/i });
    const list = page.getByRole('listbox', { name: /ingredient suggestions|zutatenvorschläge/i });

    await row.getByLabel(/^\s*(amount|menge)\s*$/i).fill('200');
    await row.getByRole('combobox', { name: /^(unit|einheit)$/i }).fill('g');
    await expect(list).toBeHidden();

    // Same start in both languages (the suite's browser is German); whole names, since Tomatenmark starts the same.
    await field.fill('Tomat');
    await expect(list.getByRole('option', { name: /^(Tomatoes|Tomaten)$/ })).toBeVisible();

    await field.press('ArrowDown');
    await field.press('Enter');
    await expect(field).toHaveValue(/^(Tomatoes|Tomaten)$/);

    await field.press('Enter');
    await expect(ingredientsOf(page).getByText(/200\s*g/)).toBeVisible();

    // Wait for the write, not "Saved" (already on screen); the suggestion comes from the database.
    await Promise.all([
      page.waitForResponse((one) => one.request().method() === 'PUT' && one.ok()),
      write(page, { amount: '1', unit: 'bunch', name: invented })
    ]);

    await expect(page.getByText(invented)).toBeVisible();

    // Nearly the whole word: earlier runs share this instance and left "Herbbutter…" rows behind.
    await field.fill(invented.slice(0, -2));
    await expect(list.getByRole('option', { name: invented })).toBeVisible();
  });

  test('takes a recipe pasted as text, after showing what it understood', async () => {
    const title = unique('Pasted');

    await page.goto('/recipes/new');
    await page.getByRole('button', { name: /paste a recipe|rezept einfügen/i }).click();

    await page
      .getByRole('textbox', { name: /the recipe, as text|rezept als text/i })
      .fill(
        [
          title,
          '',
          'Ingredients',
          '250 g flour',
          '2 eggs',
          '1-2 tbsp olive oil',
          'Salt',
          '',
          'Method',
          '1. Whisk the eggs into the flour.',
          '2. Rest the dough for half an hour.'
        ].join('\n')
      );

    await expect(page.getByRole('status')).toContainText(/4/);
    await expect(page.getByText('250 g', { exact: true })).toBeVisible();
    // A range reads as its lower bound; \s because the amount-unit space doesn't break; the browser is German.
    await expect(page.getByText(/^1\s(tbsp|EL)$/)).toBeVisible();

    await page.getByRole('button', { name: /^(review recipe|rezept prüfen)$/i }).click();
    await page
      .getByRole('dialog')
      .getByRole('button', { name: /save and edit recipe|rezept speichern und bearbeiten/i })
      .click();
    await expect(page).toHaveURL(/\/recipes\/[0-9a-f-]+\/edit/);

    await expect(ingredientsOf(page).getByText('flour')).toBeVisible();

    // Steps are fields: read their value, not the page.
    await expect(page.getByRole('combobox', { name: /step 1|schritt 1/i })).toHaveValue(
      'Whisk the eggs into the flour'
    );
    await expect(page.getByRole('combobox', { name: /step 2|schritt 2/i })).toHaveValue(
      'Rest the dough for half an hour'
    );
  });

  test('reads a recipe from a link, and says so plainly when it cannot', async () => {
    const title = unique('Linked');

    await page.goto('/recipes/new');
    await page.getByRole('button', { name: /paste a recipe|rezept einfügen/i }).click();

    const link = page.getByRole('textbox', { name: /a link to a recipe|link zum rezept/i });

    // The server fetches, so an unguarded import would read its own network; this address goes nowhere.
    await link.fill('http://169.254.169.254/latest/meta-data/');
    await page.getByRole('button', { name: /^(import recipe|rezept abrufen)$/i }).click();

    await expect(page.getByRole('alert')).toContainText(/could not be opened|nicht öffnen/i);

    // The page is stubbed; fetching itself is proven elsewhere.
    await page.route('**/api/v1/recipe-imports', (route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          sourceUrl: 'https://example.test/orzo',
          title,
          ingredientLines: ['200 g orzo', '2 courgettes'],
          steps: ['Boil the orzo.', 'Fry the courgettes.'],
          servings: 4,
          totalMinutes: 35,
          text: null
        })
      })
    );

    await link.fill('https://example.test/orzo');
    await page.getByRole('button', { name: /^(import recipe|rezept abrufen)$/i }).click();

    await expect(page.getByRole('status')).toContainText(/2/);
    await expect(page.getByText('200 g', { exact: true })).toBeVisible();

    await page.getByRole('button', { name: /^(review recipe|rezept prüfen)$/i }).click();
    await page
      .getByRole('dialog')
      .getByRole('button', { name: /save and edit recipe|rezept speichern und bearbeiten/i })
      .click();
    await expect(page).toHaveURL(/\/recipes\/[0-9a-f-]+\/edit/);

    await expect(ingredientsOf(page).getByText('orzo')).toBeVisible();
    await expect(page.getByRole('textbox', { name: /^(makes|ergibt)$/i })).toHaveValue('4');
  });

  test('shows a new recipe in the list it belongs to', async () => {
    const title = unique('Listed');

    await page.goto('/recipes/new');
    await page.getByLabel(/^\s*(title|titel)\s*$/i).fill(title);
    await page.getByRole('button', { name: /create recipe|rezept erstellen/i }).click();
    await expect(page).toHaveURL(/\/edit/);

    await page.goto('/');

    await expect(page.getByRole('link', { name: opens(title) })).toBeVisible();

    await page.getByRole('searchbox').fill(title.split(' ')[1]!);

    await expect(page.getByRole('link', { name: opens(title) })).toBeVisible();
  });

  test('does not lose what was typed to a reload, a language switch, or no signal', async ({
    context
  }) => {
    const title = unique('Kept');

    await page.goto('/recipes/new');
    await page.getByLabel(/^\s*(title|titel)\s*$/i).fill(title);
    await page.getByRole('button', { name: /create recipe|rezept erstellen/i }).click();
    await expect(page).toHaveURL(/\/edit/);

    const recipeId = new URL(page.url()).pathname.split('/')[2]!;

    await context.setOffline(true);

    await write(page, { amount: '200', unit: 'g', name: 'Butter' });

    await expect(page.getByText('Butter')).toBeVisible();

    await expect(
      page.getByText(/saved on this device|auf diesem gerät gespeichert/i)
    ).toBeVisible();

    await context.setOffline(false);
    await page.reload();

    await expect(page.getByText('Butter')).toBeVisible();
    await expect(page.getByText(/unsaved changes|nicht gespeicherten änderungen/i)).toBeVisible();

    // A language switch remounts the tree (root layout keyed by locale); typed text must survive.
    await page.goto(`/recipes/${recipeId}/edit`);
    await expect(page.getByText('Butter')).toBeVisible();
  });

  test('offers a way out when somebody else changed the same recipe', async ({ browser }) => {
    const title = unique('Contested');

    await page.goto('/recipes/new');
    await page.getByLabel(/^\s*(title|titel)\s*$/i).fill(title);
    await page.getByRole('button', { name: /create recipe|rezept erstellen/i }).click();
    await expect(page).toHaveURL(/\/edit/);

    const recipeId = new URL(page.url()).pathname.split('/')[2]!;

    const theirs = await browser.newContext({ storageState: await page.context().storageState() });
    const them = await theirs.newPage();

    await them.goto(`/recipes/${recipeId}/edit`);
    await write(them, { amount: '1', unit: 'kg', name: 'Mehl' });
    await expect(them.getByText(/^(saved|gespeichert)$/i)).toBeVisible();

    await write(page, { amount: '200', unit: 'g', name: 'Butter' });

    await expect(
      page.getByText(/someone changed this recipe|jemand hat dieses rezept geändert/i)
    ).toBeVisible();
    await expect(
      page.getByRole('button', { name: /keep my version|meine version behalten/i })
    ).toBeVisible();

    await page.getByRole('button', { name: /take theirs|andere version übernehmen/i }).click();

    // Theirs is on screen and no stale draft remains to conflict again on reload.
    // Exact: the German editor's hint says "200 g Mehl" too.
    await expect(page.getByText('Mehl', { exact: true })).toBeVisible();
    await expect(page.getByText('Butter')).toHaveCount(0);

    await page.reload();
    await expect(page.getByText('Mehl', { exact: true })).toBeVisible();
    await expect(page.getByText('Butter')).toHaveCount(0);

    await theirs.close();
  });
});
