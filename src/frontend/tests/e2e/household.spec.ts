import { expect, test, type Page } from '@playwright/test';

import {
  accountFor,
  ensureAccount,
  householdId,
  signIn,
  needsBackend,
  opens,
  seedRecipe,
  signInWithHousehold,
  skipReason,
  unique,
  writeHeaders
} from './support/culina';

/** Recipes belong to the household, personal notes do not; proven with two real browsers. */
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

    await signInWithHousehold(owner, await accountFor(browser, testInfo));

    guest = await ensureAccount(browser, `guest-${testInfo.project.name}`);

    // Shown out before the run: the invitation must be acceptable again.
    await showOthersOut();
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

    await owner.goto('/me/household');
    await owner.getByRole('button', { name: /create invitation|einladung erstellen/i }).click();

    const link = owner.getByTestId('invitation-link');

    await expect(link).toBeVisible();

    const url = new URL((await link.innerText()).trim());

    const theirContext = await browser.newContext();
    const them = await theirContext.newPage();

    await signIn(them, guest);

    await them.goto(url.pathname);
    await them.getByRole('button', { name: /join household|haushalt beitreten/i }).click();

    await expect(them).toHaveURL(/\/$/);
    await expect(them.getByRole('link', { name: opens(title) })).toBeVisible();

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

    await expect(owner.getByText(/saved|gespeichert/i)).toBeVisible();

    const theirContext = await browser.newContext();
    const them = await theirContext.newPage();

    await signIn(them, guest);
    await them.goto(`/recipes/${recipeId}`);

    await expect(them.getByRole('heading', { level: 1 })).toHaveText(title);
    await expect(them.getByText(secret)).toHaveCount(0);

    await theirContext.close();
  });

  /** Empties the household of all but its owner, so the invitation can be accepted again (a fresh account per run would fill the instance). */
  async function showOthersOut() {
    const headers = await writeHeaders(owner);
    const household = await householdId(owner.request);
    const me = await (await owner.request.get('/api/v1/users/me')).json();
    const members = await (
      await owner.request.get(`/api/v1/households/${household}/members`)
    ).json();

    for (const member of members.items as { userId: string }[]) {
      if (member.userId !== me.userId) {
        await owner.request.delete(`/api/v1/households/${household}/members/${member.userId}`, {
          headers
        });
      }
    }
  }

  test('an invitation can be taken back before anyone uses it', async () => {
    await owner.goto('/me/household');
    await owner.getByRole('button', { name: /create invitation|einladung erstellen/i }).click();

    // Only the revocable invitations: looping over members too would wait forever for a button they lack.
    const outstanding = owner.getByRole('listitem').filter({
      has: owner.getByRole('button', { name: /revoke invitation|einladung zurückziehen/i })
    });

    await expect(outstanding.first()).toBeVisible();

    // Every invitation goes, including leftovers; the empty state is the only count assertable without racing the list's loading.
    while ((await outstanding.count()) > 0) {
      await outstanding
        .first()
        .getByRole('button', { name: /revoke invitation|einladung zurückziehen/i })
        .click();
    }

    await expect(
      owner.getByText(/no pending invitations|keine offenen einladungen/i)
    ).toBeVisible();
  });
});
