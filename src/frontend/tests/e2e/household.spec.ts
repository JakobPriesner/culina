import { expect, test, type Page } from '@playwright/test';

import {
  ensureAccount,
  signIn,
  needsBackend,
  seedRecipe,
  signInWithHousehold,
  skipReason,
  unique
} from './support/culina';

/**
 * A household is the kitchen you share. What it means is that recipes belong to
 * it and personal notes do not — and that boundary is worth proving with two
 * real browsers rather than two assertions about one.
 */
test.describe.configure({ mode: 'serial' });

test.describe('sharing a kitchen', () => {
  test.skip(needsBackend, skipReason);

  let owner: Page;
  let guest: { email: string; password: string };

  test.beforeAll(async ({ browser }, testInfo) => {
    if (needsBackend) {
      return;
    }

    owner = await browser.newPage();

    await signInWithHousehold(owner);

    // A brand new person each run, not a reusable one: an invitation can only
    // be accepted by somebody who is not already in the household, and a guest
    // kept between runs is in it after the first.
    guest = await ensureAccount(
      browser,
      unique(`guest-${testInfo.project.name}`).replaceAll(' ', '-')
    );
  });

  test.afterAll(async () => {
    await owner?.close();
  });

  test('an invitation lets somebody in, and takes them to the same recipes', async ({
    browser
  }) => {
    const title = unique('Shared');

    await seedRecipe(owner, {
      title,
      yieldAmount: 2,
      ingredients: [{ quantity: 200, unit: 'g', name: 'Butter' }]
    });

    // Made from the app, not the API: the point is that a person can do this.
    await owner.goto('/me');
    await owner.getByRole('button', { name: /make an invitation|einladung erstellen/i }).click();

    const link = owner.getByTestId('invitation-link');

    await expect(link).toBeVisible();

    const url = new URL((await link.innerText()).trim());

    // Somebody else, in their own browser, who has an account of their own and
    // no household.
    const theirContext = await browser.newContext();
    const them = await theirContext.newPage();

    await signIn(them, guest);

    // Already signed in, so the link does not ask them to sign in again and
    // does not send them to a screen about not having a household. It lets
    // them in.
    await them.goto(url.pathname);

    // Landed in the household, and the recipe is simply there.
    await expect(them).toHaveURL(/\/$/);
    await expect(them.getByText(title)).toBeVisible();

    await theirContext.close();
  });

  test('a personal note stays personal', async ({ browser }) => {
    const title = unique('Noted');
    const recipeId = await seedRecipe(owner, {
      title,
      yieldAmount: 2,
      ingredients: [{ quantity: 1, unit: 'piece', name: 'Lemon' }]
    });

    const secret = unique('Halve the sugar');

    await owner.goto(`/recipes/${recipeId}`);
    await owner.locator('#overall-note').fill(secret);

    // Saved on its own, the way a note is: nobody presses Save on a note.
    await expect(owner.getByText(/saved|gespeichert/i)).toBeVisible();

    const theirContext = await browser.newContext();
    const them = await theirContext.newPage();

    await signIn(them, guest);
    await them.goto(`/recipes/${recipeId}`);

    // The recipe is shared. What one person wrote about it is not.
    await expect(them.getByRole('heading', { level: 1 })).toHaveText(title);
    await expect(them.getByText(secret)).toHaveCount(0);

    await theirContext.close();
  });

  test('an invitation can be taken back before anyone uses it', async () => {
    await owner.goto('/me');
    await owner.getByRole('button', { name: /make an invitation|einladung erstellen/i }).click();

    const outstanding = owner.getByRole('listitem');

    await expect(outstanding.first()).toBeVisible();

    // A link sent to the wrong chat has exactly one remedy, and this is it.
    // Every one of them goes, including whatever earlier runs left behind —
    // the empty state is the only count this can assert without racing the
    // list's own loading.
    while ((await outstanding.count()) > 0) {
      await outstanding
        .first()
        .getByRole('button', { name: /take it back|zurücknehmen/i })
        .click();
    }

    await expect(owner.getByText(/none waiting to be used|keine offen/i)).toBeVisible();
  });
});
