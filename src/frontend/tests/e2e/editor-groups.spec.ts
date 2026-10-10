import { expect, test } from '@playwright/test';

import { expectReflow, recipeId, responsiveData } from './support/responsive';

/** An imported recipe has several ingredient groups; the editor shows and edits every one of them. */
test.describe('the recipe editor with ingredient groups @offline', () => {
  test.use({ serviceWorkers: 'block' });

  test('shows each group under its name', async ({ page }) => {
    await responsiveData(page, 'de', { twoGroups: true });
    await page.goto(`/recipes/${recipeId}/edit`);

    await expect(page.getByRole('textbox', { name: 'Name der Gruppe 1' })).toHaveValue(
      'Für den Teig'
    );
    await expect(page.getByRole('textbox', { name: 'Name der Gruppe 2' })).toHaveValue(
      'Für die Füllung'
    );
    await expect(page.getByRole('button', { name: /Äpfel bearbeiten/ })).toBeVisible();
    await expect(
      page.getByRole('button', { name: /Sommergemüse bearbeiten/ }).first()
    ).toBeVisible();
  });

  test('sends a change to a line in the second group', async ({ page }) => {
    await responsiveData(page, 'de', { twoGroups: true });
    await page.goto(`/recipes/${recipeId}/edit`);
    await page.getByRole('button', { name: /Äpfel bearbeiten/ }).click();

    const saved = page.waitForRequest(
      (request) => request.method() === 'PUT' && request.url().endsWith(`/recipes/${recipeId}`)
    );

    await page.locator('#g1-ingredient-0-note').fill('gewürfelt');

    const groups = (await saved).postDataJSON().groups;

    expect(groups).toHaveLength(2);
    expect(groups[1].name).toBe('Für die Füllung');
    expect(groups[1].ingredients[0]).toMatchObject({ name: 'Äpfel', note: 'gewürfelt' });
    expect(groups[0].ingredients[0]).not.toHaveProperty('note', 'gewürfelt');
  });

  test('opens a line of the second group from a link to it', async ({ page }) => {
    await responsiveData(page, 'de', { twoGroups: true });
    await page.goto(`/recipes/${recipeId}/edit#ingredient-apples`);

    await expect(page.locator(':focus')).toHaveValue('Äpfel');
  });

  test('adds a group, and takes it away again while it is empty', async ({ page }) => {
    await responsiveData(page, 'de', { twoGroups: true });
    await page.goto(`/recipes/${recipeId}/edit`);
    await page.getByRole('button', { name: '+ Gruppe' }).click();

    await expect(page.getByRole('textbox', { name: 'Name der Gruppe 3' })).toBeFocused();

    await page.getByRole('button', { name: 'Leere Gruppe 3 entfernen' }).click();

    await expect(page.getByRole('textbox', { name: 'Name der Gruppe 3' })).toHaveCount(0);
  });

  test('stays inside a 320 px screen', async ({ page }) => {
    await page.setViewportSize({ width: 320, height: 720 });
    await responsiveData(page, 'de', { twoGroups: true });
    await page.goto(`/recipes/${recipeId}/edit`);
    await page.getByRole('button', { name: /Äpfel bearbeiten/ }).click();

    await expect(page.locator('#g1-ingredient-0-name')).toBeVisible();
    await expectReflow(page);
  });
});
