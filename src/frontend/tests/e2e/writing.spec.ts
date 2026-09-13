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
 * filled in when there is a minute. And an ingredient is one line, not three
 * fields — three fields is three times the tabbing and turns writing a recipe
 * into data entry. Nobody does it that way on paper either.
 */
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
    await page.getByRole('button', { name: /start the recipe|rezept anfangen/i }).click();

    // Saved and open for editing, at its own address: a recipe that exists is
    // a recipe that cannot be lost by closing a tab.
    await expect(page).toHaveURL(/\/recipes\/[0-9a-f-]+\/edit/);

    const line = page.getByLabel(/add an ingredient|zutat hinzufügen/i);

    // One line, read into an amount, a unit and a name — and shown back in
    // parts, which is the only reason guessing is safe.
    await line.fill('200 g Butter');
    await line.press('Enter');

    await expect(page.getByText('Butter')).toBeVisible();
    await expect(page.getByText(/200\s*g/)).toBeVisible();

    // A German decimal comma is a decimal point, not the note separator: a
    // person writing "1,5 kg Mehl" means one and a half kilos.
    await line.fill('1,5 kg Mehl');
    await line.press('Enter');

    await expect(page.getByText(/1[.,]5\s*kg/)).toBeVisible();

    // And what comes after a comma is how it is prepared, not what it is.
    await line.fill('2 Zwiebeln, fein gehackt');
    await line.press('Enter');

    await expect(page.getByText('Zwiebeln')).toBeVisible();
    await expect(page.getByText(/fein gehackt/)).toBeVisible();

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
    await page.getByRole('button', { name: /start the recipe|rezept anfangen/i }).click();
    await expect(page).toHaveURL(/\/edit/);

    const line = page.getByLabel(/add an ingredient|zutat hinzufügen/i);

    await line.fill('200 g Butter');
    await line.press('Enter');
    await expect(page.getByText(/200\s*g/)).toBeVisible();

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
    await page.getByRole('button', { name: /start the recipe|rezept anfangen/i }).click();
    await expect(page).toHaveURL(/\/edit/);

    const line = page.getByLabel(/add an ingredient|zutat hinzufügen/i);

    // Waited for the write itself, not for the word "Saved": that word is
    // already on screen from the save before, so it would be true too early.
    const saved = () => page.waitForResponse((one) => one.request().method() === 'PUT' && one.ok());

    // A word the built-in spellings have never heard of is not a unit yet, so
    // the whole thing lands in the name — which is the honest read of it.
    await line.fill('1 Schuss Milch');
    await Promise.all([saved(), line.press('Enter')]);

    // Corrected once, in the parts the parse is shown back as. The unit field
    // is a list you can also type into: that is how a unit is added.
    await page.getByRole('button', { name: /correct|korrigieren/i }).click();
    await page.getByRole('combobox', { name: /^(unit|einheit)$/i }).fill('Schuss');
    await page.getByRole('textbox', { name: /^(ingredient|zutat)$/i }).fill('Milch');
    await saved();

    // And from then on the kitchen knows the word.
    await line.fill('2 Schuss Sahne');
    await Promise.all([saved(), line.press('Enter')]);

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
    await page.getByRole('button', { name: /start the recipe|rezept anfangen/i }).click();
    await expect(page).toHaveURL(/\/edit/);

    const line = page.getByRole('combobox', { name: /add an ingredient|zutat hinzufügen/i });
    const list = page.getByRole('listbox', { name: /ingredient suggestions|zutatenvorschläge/i });

    // Nothing is suggested for the amount: nobody needs help typing "200 g".
    await line.fill('200 g ');
    await expect(list).toBeHidden();

    // The seeded list is what an empty kitchen has, and it says where the
    // thing lives in a shop.
    await line.fill('200 g Potat');
    await expect(list.getByRole('option', { name: /^Potatoes/ })).toBeVisible();

    // Tab takes the suggestion and leaves the amount alone; Enter would have
    // finished the line, which is what Enter has always done here.
    await line.press('Tab');
    await expect(line).toHaveValue('200 g Potatoes');
    await line.press('Enter');

    // A word no seeded list has ever heard of is still an ingredient.
    await line.fill(`1 bunch ${invented}`);
    await line.press('Enter');

    await expect(page.getByText(invented)).toBeVisible();
    await expect(page.getByText(/^(saved|gespeichert)$/i)).toBeVisible();

    // And from then on it is one of this kitchen's own words.
    await line.fill(invented.slice(0, 8));
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

    await page.getByRole('button', { name: /make this recipe|rezept anlegen/i }).click();
    await expect(page).toHaveURL(/\/recipes\/[0-9a-f-]+\/edit/);

    await expect(page.getByText('flour')).toBeVisible();

    // The steps are fields, so this reads their value rather than the page.
    // The numbers were the paste's; the editor's list supplies its own.
    await expect(page.getByRole('combobox', { name: /step 1|schritt 1/i })).toHaveValue(
      'Whisk the eggs into the flour'
    );
    await expect(page.getByRole('combobox', { name: /step 2|schritt 2/i })).toHaveValue(
      'Rest the dough for half an hour'
    );
  });

  test('shows a new recipe in the list it belongs to', async () => {
    const title = unique('Listed');

    await page.goto('/recipes/new');
    await page.getByLabel(/^\s*(title|titel)\s*$/i).fill(title);
    await page.getByRole('button', { name: /start the recipe|rezept anfangen/i }).click();
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
    await page.getByRole('button', { name: /start the recipe|rezept anfangen/i }).click();
    await expect(page).toHaveURL(/\/edit/);

    const recipeId = new URL(page.url()).pathname.split('/')[2]!;
    const line = page.getByLabel(/add an ingredient|zutat hinzufügen/i);

    // Offline, which is the case the journal exists for: nothing can reach the
    // server, and what was typed must still be there afterwards.
    await context.setOffline(true);

    await line.fill('200 g Butter');
    await line.press('Enter');

    await expect(page.getByText('Butter')).toBeVisible();

    // The app says where the work is, and does not say "Saved" about something
    // that only exists on this laptop.
    await expect(page.getByText(/kept on this device|auf diesem gerät/i)).toBeVisible();

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
    await page.getByRole('button', { name: /start the recipe|rezept anfangen/i }).click();
    await expect(page).toHaveURL(/\/edit/);

    const recipeId = new URL(page.url()).pathname.split('/')[2]!;

    // The other half of the household, writing at the same time.
    const theirs = await browser.newContext({ storageState: await page.context().storageState() });
    const them = await theirs.newPage();

    await them.goto(`/recipes/${recipeId}/edit`);
    await them.getByLabel(/add an ingredient|zutat hinzufügen/i).fill('1 kg Mehl');
    await them.getByLabel(/add an ingredient|zutat hinzufügen/i).press('Enter');
    await expect(them.getByText(/^(saved|gespeichert)$/i)).toBeVisible();

    // This browser is now writing on top of a version that has moved on.
    const line = page.getByLabel(/add an ingredient|zutat hinzufügen/i);

    await line.fill('200 g Butter');
    await line.press('Enter');

    // Said plainly, and neither version is thrown away: both choices are here.
    await expect(page.getByText(/somebody else changed|jemand anderes/i)).toBeVisible();
    await expect(
      page.getByRole('button', { name: /keep my version|meine fassung/i })
    ).toBeVisible();

    await page.getByRole('button', { name: /take theirs|ihre übernehmen/i }).click();

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
