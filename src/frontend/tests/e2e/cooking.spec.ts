import { expect, test, type Page } from '@playwright/test';

import {
  accountFor,
  needsBackend,
  seedRecipe,
  signInWithHousehold,
  skipReason,
  unique
} from './support/culina';

/**
 * Cooking, which is the one screen used with wet hands and no attention to
 * spare.
 *
 * The transition into it matters more than the screen does: the same surface
 * with a different emphasis, so nothing a person was looking at jumps somewhere
 * else. And leaving it must not lose the place — a phone that locked itself
 * between step two and step three is the normal case, not an edge one.
 */
// One browser for the file, so the tests share a session and a cook session.
test.describe.configure({ mode: 'serial' });

test.describe('cooking a recipe', () => {
  test.skip(needsBackend, skipReason);

  /*
   * Its own account, per project, signed into once for the whole file.
   *
   * Its own, because only one recipe can be being cooked at a time — the
   * database says so, and it is the right rule — so a desktop and a mobile
   * browser sharing one account are two browsers taking the cook session away
   * from each other.
   *
   * Once, because signing in is rate limited per account, as it should be. A
   * suite that signs in for every test is a suite that locks itself out.
   */
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

    // The step lands on screen at once and reaches the server behind it. Both
    // are waited for here: the first is what the cook sees, the second is what
    // survives the page going away a moment later. A phone that locks keeps the
    // page alive and the request with it; a hard navigation this fast does not,
    // and that is the test's impatience rather than the app's problem.
    await Promise.all([
      // Any answer, not only a successful one: waiting for `ok` here means
      // waiting forever when the answer is something else, and hiding what it
      // was behind a timeout. Whether the move stuck is asserted below, on the
      // screen, which is where it matters.
      page.waitForResponse(
        (response) =>
          response.url().includes('/api/v1/cook-sessions/') &&
          response.request().method() === 'PATCH'
      ),
      next.click()
    ]);

    await expect(page.getByText(/step 2 of 3|schritt 2 von 3/i)).toBeVisible();

    // The phone goes away — a different screen, a lock, a call.
    await page.goto('/shopping');

    // And the bar says what is still going on, from anywhere in the app.
    const bar = page.getByText(new RegExp(`cooking ${title}|Gerade am Kochen: ${title}`, 'i'));

    await expect(bar).toBeVisible();

    await page.getByRole('link', { name: /keep cooking|weiterkochen/i }).click();

    // Back at step two, not back at the beginning.
    await expect(page).toHaveURL(new RegExp(`/recipes/${recipeId}/cook`));
    await expect(page.getByText(/step 2 of 3|schritt 2 von 3/i)).toBeVisible();

    // Through the last step, where the primary action becomes finishing.
    await next.click();
    await expect(page.getByText(/step 3 of 3|schritt 3 von 3/i)).toBeVisible();

    await page.getByRole('button', { name: /^(i made it|fertig gekocht)$/i }).click();

    // And the bar is gone, because nothing is being cooked any more.
    await expect(bar).toHaveCount(0);
  });

  test('scales while cooking, and the step text scales with it', async () => {
    const recipeId = await seedRecipe(page, {
      title: unique('Cooking scale'),
      yieldAmount: 2,
      ingredients: [{ quantity: 200, unit: 'g', name: 'Butter' }],
      steps: ['Melt {0}.']
    });

    // Straight into cooking at a different yield, the way a shared link would.
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

    // Measured, because it was measurably wrong: "Previous step" is a longer
    // phrase than "Next step", and the control that undoes progress used to be
    // the wider of the two. Next is pressed once per step with a wet thumb and
    // an eye on a pan; previous is pressed when something went wrong.
    expect(next!.width).toBeGreaterThan(previous!.width * 2);

    // And neither is ever below the size a thumb can find.
    expect(Math.min(next!.height, previous!.height)).toBeGreaterThanOrEqual(44);
    expect(Math.min(next!.width, previous!.width)).toBeGreaterThanOrEqual(44);
  });
});
