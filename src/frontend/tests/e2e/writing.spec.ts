import { expect, test, type Page } from '@playwright/test';

import {
  accountFor,
  needsBackend,
  signInWithHousehold,
  skipReason,
  unique
} from './support/culina';

/**
 * Writing a recipe down, which is the part people abandon.
 *
 * A recipe needs a title and nothing else to exist; everything after that is
 * filled in when there is a minute. An ingredient is written as the three
 * things it is made of — an amount, a unit and a name — because each is used on
 * its own afterwards: the amount scales, the unit converts, and the name is
 * what reaches a shopping list.
 */
/** The row of empty fields at the foot of the list, where the next one goes. */
/**
 * The ingredients a recipe actually has, as opposed to the advice about them.
 *
 * The hint under the fields offers "200 g flour" as its example and sits in
 * the same region as the list, so an assertion about an amount finds the
 * suggestion too and fails on two matches rather than none.
 */
const ingredientsOf = (page: Page) =>
  page.getByRole('region', { name: /^(ingredients|zutaten)$/i }).getByRole('list');

const newIngredient = (page: Page) =>
  page.getByRole('group', { name: /new ingredient|neue zutat/i });

interface Written {
  readonly amount?: string;
  readonly unit?: string;
  readonly name: string;
  readonly note?: string;
}

/**
 * Writes one ingredient into the fields it is made of.
 *
 * The name goes last and carries the Enter, because the name is what makes the
 * row an ingredient: there is nothing to add until it is there.
 */
async function write(page: Page, { amount = '', unit = '', name, note = '' }: Written) {
  const row = newIngredient(page);

  // Padded, like every other getByLabel here: the label wraps its text in a
  // span beside the input, so what Playwright matches carries the whitespace
  // between them and an anchored pattern never fires.
  await row.getByLabel(/^\s*(amount|menge)\s*$/i).fill(amount);
  await row.getByRole('combobox', { name: /^(unit|einheit)$/i }).fill(unit);
  await row.getByLabel(/^\s*(note|hinweis)/i).fill(note);

  // Not anchored at the end: once a suggestion is arrowed to, the field's
  // accessible name becomes "Ingredient Potatoes" — the highlighted option is
  // part of what a screen reader says — and a locator that insisted on the
  // label alone stopped matching the control it was already typing into.
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

    // Saved and open for editing, at its own address: a recipe that exists is
    // a recipe that cannot be lost by closing a tab.
    await expect(page).toHaveURL(/\/recipes\/[0-9a-f-]+\/edit/);

    // Three fields, and the ingredient reads back as the one line a recipe
    // would print it as.
    await write(page, { amount: '200', unit: 'g', name: 'Butter' });

    await expect(ingredientsOf(page).getByText('Butter')).toBeVisible();
    await expect(ingredientsOf(page).getByText(/200\s*g/)).toBeVisible();

    // A German decimal comma is a decimal point: somebody typing "1,5" into
    // the amount means one and a half.
    await write(page, { amount: '1,5', unit: 'kg', name: 'Mehl' });

    await expect(page.getByText(/1[.,]5\s*kg/)).toBeVisible();

    // How it is prepared is its own field, and is not what the thing is.
    await write(page, { amount: '2', name: 'Zwiebeln', note: 'fein gehackt' });

    await expect(page.getByText('Zwiebeln')).toBeVisible();
    await expect(page.getByText(/fein gehackt/)).toBeVisible();

    // The fields are empty again, waiting for the next one.
    await expect(newIngredient(page).getByLabel(/^\s*(amount|menge)\s*$/i)).toHaveValue('');

    // Typed, never submitted: an editor that loses work on a closed tab is an
    // editor nobody trusts with a recipe they are still thinking about.
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

    // Saved before the mention is written, because a line the server has never
    // seen has no id for a step to point at.
    await expect(page.getByText(/^(saved|gespeichert)$/i)).toBeVisible();

    await page.getByRole('button', { name: /add a step|schritt hinzufügen/i }).click();

    const step = page.getByRole('combobox', { name: /step 1|schritt 1/i });

    // The whole of the ceremony: an @, then the keyboard.
    await step.fill('Schmilz @But');
    await expect(page.getByRole('option', { name: /Butter/ })).toBeVisible();
    await step.press('Enter');
    await expect(step).toHaveValue('Schmilz @Butter ');

    await step.pressSequentially('in der Pfanne.');
    await expect(page.getByText(/^(saved|gespeichert)$/i)).toBeVisible();

    // And what it bought: the amount is inside the sentence, and it follows
    // the portions rather than sitting there as a number somebody typed.
    await page.goto(`/recipes/${new URL(page.url()).pathname.split('/')[2]}`);

    const method = page.getByRole('region', { name: /^(steps|zubereitung)$/i });

    await expect(method).toContainText(/200\s*g\s*Butter/);

    // Written for four, read at five: the amount in the sentence is derived,
    // not a number somebody typed into it.
    await page.getByRole('button', { name: /^(one more|eine mehr)$/i }).click();

    await expect(method).toContainText(/250\s*g\s*Butter/);
  });

  test('measures in a unit this kitchen invented, and scales it', async () => {
    const title = unique('Schuss');

    await page.goto('/recipes/new');
    await page.getByLabel(/^\s*(title|titel)\s*$/i).fill(title);
    await page.getByRole('button', { name: /create recipe|rezept erstellen/i }).click();
    await expect(page).toHaveURL(/\/edit/);

    // Waited for the write itself, not for the word "Saved": that word is
    // already on screen from the save before, so it would be true too early.
    const saved = () => page.waitForResponse((one) => one.request().method() === 'PUT' && one.ok());

    // The unit field is a list you can also type into, which is the whole
    // reason the vocabulary is open: a word nobody has written before becomes
    // a unit by being written, with nothing to correct afterwards.
    await Promise.all([saved(), write(page, { amount: '1', unit: 'Schuss', name: 'Milch' })]);

    // And from then on the kitchen knows the word — it is on the list before
    // the whole of it has been typed.
    const unit = newIngredient(page).getByRole('combobox', { name: /^(unit|einheit)$/i });

    await unit.fill('Schu');
    await expect(
      page
        .getByRole('listbox', { name: /unit suggestions|einheitenvorschläge/i })
        .getByRole('option', { name: 'Schuss' })
    ).toBeVisible();

    await Promise.all([saved(), write(page, { amount: '2', unit: 'Schuss', name: 'Sahne' })]);

    await page.goto(`/recipes/${new URL(page.url()).pathname.split('/')[2]}`);

    const list = page.getByRole('region', { name: /ingredients|zutaten/i });

    await expect(list).toContainText(/1\s*Schuss/);
    await expect(list).toContainText(/2\s*Schuss/);

    // It scales with the portions like any other unit it counts in, rounding
    // the way a cook writes rather than to a quarter of a splash — and it
    // converts to nothing, because nobody knows how much a Schuss is.
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
    // Not anchored at the end: once a suggestion is arrowed to, the field's
    // accessible name becomes "Ingredient Potatoes" — the highlighted option is
    // part of what a screen reader says — and a locator that insisted on the
    // label alone stopped matching the control it was already typing into.
    const field = row.getByRole('combobox', { name: /^\s*(ingredient|zutat)\b/i });
    const list = page.getByRole('listbox', { name: /ingredient suggestions|zutatenvorschläge/i });

    // Only the name is suggested. The amount is its own field now, and nobody
    // needs help typing a number into it.
    await row.getByLabel(/^\s*(amount|menge)\s*$/i).fill('200');
    await row.getByRole('combobox', { name: /^(unit|einheit)$/i }).fill('g');
    await expect(list).toBeHidden();

    // The seeded list is what an empty kitchen has, and it says where the
    // thing lives in a shop.
    await field.fill('Potat');
    await expect(list.getByRole('option', { name: /^Potatoes/ })).toBeVisible();

    // Arrowing to a row and pressing Enter takes it. Enter on its own adds
    // what was typed, which is what stops the list from overruling anybody.
    await field.press('ArrowDown');
    await field.press('Enter');
    await expect(field).toHaveValue('Potatoes');

    await field.press('Enter');
    await expect(ingredientsOf(page).getByText(/200\s*g/)).toBeVisible();

    // A word no seeded list has ever heard of is still an ingredient.
    //
    // Waited for by the write, not by the word "Saved": that word is already on
    // screen from the save before, and the suggestion below comes from what
    // this household's recipes actually say — which means from the database.
    await Promise.all([
      page.waitForResponse((one) => one.request().method() === 'PUT' && one.ok()),
      write(page, { amount: '1', unit: 'bunch', name: invented })
    ]);

    await expect(page.getByText(invented)).toBeVisible();

    // And from then on it is one of this kitchen's own words.
    // Nearly the whole word: these suites share one instance, and every
    // earlier run of this test left a "Herbbutter…" of its own behind. A
    // prefix they all share is a prefix that finds ten of them.
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

    // The preview is the feature. What was understood is on screen before a
    // recipe exists, so a wrong reading costs a keystroke rather than a delete.
    await expect(page.getByRole('status')).toContainText(/4/);
    await expect(page.getByText('250 g', { exact: true })).toBeVisible();
    // A range is read as its lower bound: the one you can still add to.
    await expect(page.getByText('1 tbsp', { exact: true })).toBeVisible();

    // The one in the paste panel, not the form's own: both say "Create recipe"
    // since the copy pass, and the form's is the empty-recipe button this
    // screen also carries.
    await page
      .getByRole('button', { name: /create recipe|rezept erstellen/i })
      .last()
      .click();
    await expect(page).toHaveURL(/\/recipes\/[0-9a-f-]+\/edit/);

    await expect(ingredientsOf(page).getByText('flour')).toBeVisible();

    // The steps are fields, so this reads their value rather than the page.
    // The numbers were the paste's; the editor's list supplies its own.
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

    // The refusal first, and against a real address: the server does the
    // fetching, so an unguarded import would read the network it sits in. This
    // one goes nowhere, and says so without saying what it found.
    await link.fill('http://169.254.169.254/latest/meta-data/');
    await page.getByRole('button', { name: /^(import recipe|rezept abrufen)$/i }).click();

    await expect(page.getByRole('alert')).toContainText(/could not be opened|nicht öffnen/i);

    // And the ordinary case. The page itself is stubbed here — what the server
    // does with an address is proven where the fetching is — so this is about
    // the draft arriving and landing in the same preview a paste does.
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

    // The import panel's, as in the pasted case: the form's own empty-recipe
    // button carries the same words.
    await page
      .getByRole('button', { name: /create recipe|rezept erstellen/i })
      .last()
      .click();
    await expect(page).toHaveURL(/\/recipes\/[0-9a-f-]+\/edit/);

    await expect(ingredientsOf(page).getByText('orzo')).toBeVisible();
    // What the site published about the recipe, not only its words.
    await expect(page.getByRole('textbox', { name: /^(makes|ergibt)$/i })).toHaveValue('4');
  });

  test('shows a new recipe in the list it belongs to', async () => {
    const title = unique('Listed');

    await page.goto('/recipes/new');
    await page.getByLabel(/^\s*(title|titel)\s*$/i).fill(title);
    await page.getByRole('button', { name: /create recipe|rezept erstellen/i }).click();
    await expect(page).toHaveURL(/\/edit/);

    await page.goto('/');

    await expect(page.getByText(title)).toBeVisible();

    // And search finds it, which is what a list is for once there are forty.
    await page.getByRole('searchbox').fill(title.split(' ')[1]!);

    await expect(page.getByText(title)).toBeVisible();
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

    // Offline, which is the case the journal exists for: nothing can reach the
    // server, and what was typed must still be there afterwards.
    await context.setOffline(true);

    await write(page, { amount: '200', unit: 'g', name: 'Butter' });

    await expect(page.getByText('Butter')).toBeVisible();

    // The app says where the work is, and does not say "Saved" about something
    // that only exists on this laptop.
    await expect(
      page.getByText(/saved on this device|auf diesem gerät gespeichert/i)
    ).toBeVisible();

    await context.setOffline(false);
    await page.reload();

    // Still there, and still described honestly.
    await expect(page.getByText('Butter')).toBeVisible();
    await expect(page.getByText(/unsaved changes|nicht gespeicherte änderungen/i)).toBeVisible();

    // A language switch remounts the whole tree — the root layout is keyed by
    // locale so that compiled messages take effect without a reload — and the
    // editor's state goes with it. What was typed does not.
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

    // The other half of the household, writing at the same time.
    const theirs = await browser.newContext({ storageState: await page.context().storageState() });
    const them = await theirs.newPage();

    await them.goto(`/recipes/${recipeId}/edit`);
    await write(them, { amount: '1', unit: 'kg', name: 'Mehl' });
    await expect(them.getByText(/^(saved|gespeichert)$/i)).toBeVisible();

    // This browser is now writing on top of a version that has moved on.
    await write(page, { amount: '200', unit: 'g', name: 'Butter' });

    // Said plainly, and neither version is thrown away: both choices are here.
    await expect(
      page.getByText(/someone changed this recipe|jemand hat dieses rezept geändert/i)
    ).toBeVisible();
    await expect(
      page.getByRole('button', { name: /keep my version|meine version behalten/i })
    ).toBeVisible();

    await page.getByRole('button', { name: /take theirs|andere version übernehmen/i }).click();

    // Theirs is what is on screen, and this device is no longer holding a draft
    // that would come back on the next reload and conflict all over again.
    await expect(page.getByText('Mehl')).toBeVisible();
    await expect(page.getByText('Butter')).toHaveCount(0);

    await page.reload();
    await expect(page.getByText('Mehl')).toBeVisible();
    await expect(page.getByText('Butter')).toHaveCount(0);

    await theirs.close();
  });
});
