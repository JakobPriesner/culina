import { expect, test, type Page } from '@playwright/test';

import {
  accountFor,
  needsBackend,
  seedRecipe,
  signInWithHousehold,
  skipReason,
  unique
} from './support/culina';

/** Cooking is the wet-hands screen: the transition must not make anything jump, and leaving must not lose the place (a locked phone is the normal case). */
// One browser for the file, so the tests share a session and a cook session.
test.describe.configure({ mode: 'serial' });

test.describe('cooking a recipe', () => {
  test.skip(needsBackend, skipReason);

  /* One account per project, signed in once for the file: only one recipe can be cooked at a time per account, and sign-in is rate limited. */
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

  test('keeps the place through leaving the page and coming back', async () => {
    const title = unique('Cooking');
    const recipeId = await seedRecipe(page, {
      title,
      yieldAmount: 2,
      ingredients: [{ quantity: 200, unit: 'g', name: 'Butter' }],
      steps: ['Melt {0}.', 'Wait for it to foam.', 'Take it off the heat.']
    });

    await page.goto(`/recipes/${recipeId}`);
    await page.getByRole('button', { name: /^(start cooking|kochen starten)$/i }).click();

    await expect(page).toHaveURL(new RegExp(`/recipes/${recipeId}/cook`));

    const next = page.getByRole('button', { name: /^(next step|nächster schritt)$/i });

    // Arrow keys belong to the focused servings control; changing a field must not leave the step.
    await page.getByRole('spinbutton', { name: /servings|portionen/i }).focus();
    await page.keyboard.press('ArrowRight');
    await expect(page.getByText(/step 1 of 3|schritt 1 von 3/i)).toBeVisible();

    // Wait for both the screen and the server request: a hard navigation this fast would drop the request (a locked phone would not).
    await Promise.all([
      // Any answer, not only `ok`: waiting for ok hangs on anything else; the move is asserted on screen below.
      page.waitForResponse(
        (response) =>
          response.url().includes('/api/v1/cook-sessions/') &&
          response.request().method() === 'PATCH'
      ),
      next.click()
    ]);

    await expect(page.getByText(/step 2 of 3|schritt 2 von 3/i)).toBeVisible();
    await expect(page.locator('[aria-current="step"]')).toBeFocused();

    await page.goto('/shopping');

    const bar = page.getByText(new RegExp(`cooking ${title}|Gerade am Kochen: ${title}`, 'i'));

    await expect(bar).toBeVisible();

    await page.getByRole('link', { name: /keep cooking|weiterkochen/i }).click();

    await expect(page).toHaveURL(new RegExp(`/recipes/${recipeId}/cook`));
    await expect(page.getByText(/step 2 of 3|schritt 2 von 3/i)).toBeVisible();

    await next.click();
    await expect(page.getByText(/step 3 of 3|schritt 3 von 3/i)).toBeVisible();

    await page.getByRole('button', { name: /^(i made it|fertig gekocht)$/i }).click();

    await expect(bar).toHaveCount(0);
  });

  test('scales while cooking, and the step text scales with it', async () => {
    const recipeId = await seedRecipe(page, {
      title: unique('Cooking scale'),
      yieldAmount: 2,
      ingredients: [{ quantity: 200, unit: 'g', name: 'Butter' }],
      steps: ['Melt {0}.']
    });

    // Straight into cooking at a different yield, like a shared link.
    await page.goto(`/recipes/${recipeId}/cook?yield=4`);

    await expect(page.getByRole('main')).toContainText('400');

    await page.getByRole('button', { name: /^(one more|eine mehr)$/i }).click();

    await expect(page.getByRole('main')).toContainText('500');
  });

  test('offers a timer for a step that waits', async () => {
    const recipeId = await seedRecipe(page, {
      title: unique('Timed'),
      yieldAmount: 2,
      ingredients: [{ quantity: 1, unit: 'piece', name: 'Onion' }],
      steps: ['Soften {0} slowly.']
    });

    await page.goto(`/recipes/${recipeId}/edit`);

    const saved = page.waitForResponse(
      (response) =>
        response.url().includes(`/api/v1/recipes/${recipeId}`) &&
        response.request().method() === 'PUT'
    );

    await page.getByRole('spinbutton', { name: /timer for step 1|timer für schritt 1/i }).fill('2');
    await saved;

    await page.goto(`/recipes/${recipeId}/cook`);

    await expect(
      page.getByRole('button', { name: /start 2 min timer|timer über 2 min\. starten/i })
    ).toBeVisible();
  });

  test('keeps private notes within reach while cooking', async () => {
    const note = unique('Use the heavy pan');
    const recipeId = await seedRecipe(page, {
      title: unique('Cook with notes'),
      ingredients: [{ quantity: 1, unit: 'piece', name: 'Onion' }],
      steps: ['Soften {0}.']
    });

    await page.goto(`/recipes/${recipeId}`);

    const notes = page.getByRole('region', { name: /your notes|deine notizen/i });

    await notes.getByRole('textbox').fill(note);
    await expect(notes.getByRole('status')).toContainText(/saved|gespeichert/i);

    await page.getByRole('button', { name: /^(start cooking|kochen starten)$/i }).click();
    await page.getByRole('button', { name: /your notes|deine notizen/i }).click();

    const sheet = page.getByRole('dialog', { name: /your notes|deine notizen/i });

    await expect(sheet.getByRole('textbox')).toHaveValue(note);

    await sheet.getByRole('button', { name: /^(close|schließen)$/i }).click();
    await page.getByRole('button', { name: /^(done cooking|fertig gekocht)$/i }).click();
  });

  test('puts the biggest target under the thumb that is pressed most', async () => {
    const recipeId = await seedRecipe(page, {
      title: unique('Wet hands'),
      yieldAmount: 2,
      ingredients: [{ quantity: 200, unit: 'g', name: 'Butter' }],
      steps: ['Melt {0}.', 'Stir it.', 'Rest it.']
    });

    await page.setViewportSize({ width: 320, height: 720 });
    await page.goto(`/recipes/${recipeId}/cook`);
    await expect(page.getByText(/step 1 of 3|schritt 1 von 3/i)).toBeVisible();

    const next = await page
      .getByRole('button', { name: /^(next step|nächster schritt)$/i })
      .boundingBox();
    const previous = await page
      .getByRole('button', { name: /^(previous step|vorheriger schritt)$/i })
      .boundingBox();

    // Measured: "Previous step" is a longer phrase than "Next step" and used to make the undo control the wider one.
    expect(next!.width).toBeGreaterThan(previous!.width * 2);

    expect(Math.min(next!.height, previous!.height)).toBeGreaterThanOrEqual(44);
    expect(Math.min(next!.width, previous!.width)).toBeGreaterThanOrEqual(44);
  });
});
