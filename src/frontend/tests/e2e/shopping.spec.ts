import { expect, test, type Page } from '@playwright/test';

/**
 * The shopping list, the way it is used: one hand, one line at a time.
 *
 * The keyboard path is the point. A one-line add is only a one-line add if
 * Enter sends it — reaching for the button after every item is the data entry
 * this screen exists to avoid — and a synthetic key event cannot prove that.
 * Playwright presses a real key.
 *
 * Skipped without a backend, because a shopping list with nothing behind it is
 * a page that cannot be wrong.
 */
const email = process.env['CULINA_E2E_EMAIL'];
const password = process.env['CULINA_E2E_PASSWORD'];

/**
 * A name no other run can be holding.
 *
 * The list belongs to a household, and every project and worker shares one
 * account: a test that asserts on "Salz" is asserting on whatever the browser
 * next to it is doing.
 */
const unique = (word: string) =>
  `${word} ${Date.now().toString(36)}${Math.random().toString(36).slice(2, 6)}`;

async function signIn(page: Page) {
  await page.goto('/login');
  await page.getByLabel(/email|e-mail/i).fill(email!);
  await page.getByLabel(/password|passwort/i).fill(password!);
  await page.getByRole('button', { name: /^(sign in|anmelden)$/i }).click();

  await expect(page).toHaveURL(/\/(welcome)?$/);

  // An account can exist without a household — registration allows it, and the
  // list belongs to a household rather than to a person. The first run of this
  // suite against a fresh account lands here; later ones do not.
  if (new URL(page.url()).pathname === '/welcome') {
    await page.getByLabel(/name your household|heißen/i).fill('E2E kitchen');
    await page.getByRole('button', { name: /^(create a household|haushalt anlegen)$/i }).click();

    await expect(page).toHaveURL(/\/$/);
  }
}

test.describe('the shopping list', () => {
  test.skip(
    !email || !password,
    'Set CULINA_E2E_EMAIL and CULINA_E2E_PASSWORD with a backend running.'
  );

  test('takes a written line on Enter, with its unit intact', async ({ page }) => {
    await signIn(page);
    await page.goto('/shopping');

    const field = page.getByRole('textbox', { name: /add|hinzufügen/i });
    const name = unique('Feta');

    await field.fill(`2 Packungen ${name}`);
    await field.press('Enter');

    // The field empties, which is the signal that the line was taken: the next
    // item can be typed straight away.
    await expect(field).toHaveValue('');

    const row = page.getByRole('listitem').filter({ hasText: name });

    await expect(row).toBeVisible();
    // "2" alone would be a different shopping trip. The unit is not decoration.
    await expect(row).toContainText(/2\s*(Packungen|packs)/);

    await row.getByRole('button', { name: /remove|entfernen/i }).click();
    await expect(row).toHaveCount(0);
  });

  test('keeps its actions clear of the bars pinned to the bottom', async ({ page }) => {
    await signIn(page);
    await page.goto('/shopping');

    const field = page.getByRole('textbox', { name: /add|hinzufügen/i });
    const name = unique('Salz');

    await field.fill(`1 Prise ${name}`);
    await field.press('Enter');
    await expect(field).toHaveValue('');

    const row = page.getByRole('listitem').filter({ hasText: name });

    await expect(row).toBeVisible();

    // A row the bottom navigation covers cannot be ticked off, and on a phone
    // that is the whole screen. Scrolled to the end of the list, the last row
    // has to be above whatever the shell has parked there.
    await page.mouse.wheel(0, 5000);

    const box = (await row.boundingBox())!;
    const bottomOfViewport = page.viewportSize()!.height;
    const covered = await page.evaluate(
      ([y, height]) => {
        const at = document.elementFromPoint(20, y! + height! / 2);

        return at ? !at.closest('main') : true;
      },
      [box.y, box.height]
    );

    expect(box.y).toBeLessThan(bottomOfViewport);
    expect(covered, 'the row is behind a bar').toBe(false);

    await row.getByRole('button', { name: /remove|entfernen/i }).click();
  });
});
