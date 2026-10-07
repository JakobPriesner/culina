import { expect, test, type Page } from '@playwright/test';

import {
  accountFor,
  needsBackend,
  opens,
  seedRecipe,
  signInWithHousehold,
  skipReason,
  unique,
  writeHeaders
} from './support/culina';

/** A second household inheriting the first's recipes: made from the header, switched to at once, showing them readable but not changeable. */
test.describe('a household that inherits another', () => {
  test.skip(needsBackend, skipReason);

  test('is made from the switcher and shows the other kitchen’s recipes, read-only', async ({
    page,
    browser
  }, testInfo) => {
    await signInWithHousehold(page, await accountFor(browser, testInfo));

    // Households a previous run left untidied, so the account is back to the one seeded kitchen.
    await removeFlats(page);

    const title = unique('Inherited');
    const recipeId = await seedRecipe(page, {
      title,
      ingredients: [{ quantity: 200, unit: 'g', name: 'Butter' }]
    });
    const kitchen = await kitchenOf(page, recipeId);
    const flat = unique('Flat');

    try {
      await page.goto('/');
      await page.getByRole('button', { name: /switch household|haushalt wechseln/i }).click();
      await page.getByRole('button', { name: /^(new household|neuer haushalt)$/i }).click();

      await page.getByRole('textbox', { name: /^name/i }).fill(flat);
      await page
        .getByRole('combobox', {
          name: /inherits recipes from|erbt rezepte von/i
        })
        .selectOption({ label: kitchen });
      await page
        .getByRole('button', { name: /^(create a household|haushalt erstellen)$/i })
        .click();

      await expect(
        page.getByRole('button', {
          name: new RegExp(`^${flat}, (switch household|haushalt wechseln)`, 'i')
        })
      ).toBeVisible();

      await page.getByRole('searchbox').first().fill(title);
      const card = page
        .getByRole('listitem')
        .filter({ has: page.getByRole('link', { name: opens(title) }) });

      await expect(card).toContainText(new RegExp(`(from|aus) ${kitchen}`, 'i'));

      await card.getByRole('link').first().click();

      await expect(page.getByText(/can change it|ändern kann es aber nur/i)).toBeVisible();

      await page.getByRole('button', { name: /^(more actions|weitere aktionen)$/i }).click();
      await expect(page.getByRole('button', { name: /^(add to plan|einplanen)$/i })).toBeVisible();
      await expect(page.getByRole('link', { name: /^(edit|bearbeiten)$/i })).toHaveCount(0);
      await expect(
        page.getByRole('button', { name: /^(delete recipe|rezept löschen)$/i })
      ).toHaveCount(0);
      await expect(page.getByRole('button', { name: /^(share|teilen)$/i })).toHaveCount(0);
      await page.keyboard.press('Escape');

      await page
        .getByRole('complementary')
        .getByRole('button', { name: /^(make my own copy|eigene kopie anlegen)$/i })
        .click();
      await expect(page).toHaveURL(/\/recipes\/[^/]+\/edit$/);
      await expect(page.getByRole('textbox', { name: /^(title|titel)/i })).toHaveValue(title);

      await page.getByRole('button', { name: /switch household|haushalt wechseln/i }).click();
      await page.getByRole('button', { name: kitchen, exact: true }).click();
      await page.goto('/me/household');
      await expect(page.getByRole('listitem').filter({ hasText: flat })).toBeVisible();
    } finally {
      await removeFlats(page);
    }
  });
});

/** The name of the household a recipe belongs to. */
async function kitchenOf(page: Page, recipeId: string): Promise<string> {
  const recipe = await (await page.request.get(`/api/v1/recipes/${recipeId}`)).json();
  const me = await (await page.request.get('/api/v1/users/me')).json();

  return me.households.find(
    (household: { householdId: string }) => household.householdId === recipe.householdId
  ).name;
}

async function removeFlats(page: Page): Promise<void> {
  const headers = await writeHeaders(page);
  const me = await (await page.request.get('/api/v1/users/me')).json();

  for (const household of me.households as { householdId: string; name: string }[]) {
    if (household.name.startsWith('Flat ')) {
      const path = `/api/v1/households/${household.householdId}`;
      const etag = (await page.request.get(path)).headers()['etag'] ?? '';
      const removed = await page.request.delete(path, {
        headers: { ...headers, 'If-Match': etag }
      });

      expect(removed.ok(), await removed.text()).toBe(true);
    }
  }
}
