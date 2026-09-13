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
