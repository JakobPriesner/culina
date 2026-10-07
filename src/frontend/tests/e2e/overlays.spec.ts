import { expect, test } from '@playwright/test';

/**
 * Focus trap, restoration and Escape are the browser's own `<dialog>` behaviour, hence tested in a
 * real browser (jsdom has no top layer).
 */
test.describe('overlays @offline', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/design');
  });

  test('never lets focus reach a control behind it', async ({ page }) => {
    await page.getByRole('button', { name: 'Open modal' }).click();

    await expect(page.getByRole('dialog')).toBeVisible();

    /* Focus briefly sits on <body> when tabbing wraps; it must never land behind the dialog. */
    const focused = async () =>
      page.evaluate(() => {
        const element = document.activeElement;

        return {
          tag: element?.tagName ?? '',
          inside: element?.closest('dialog') !== null,
          name: element?.textContent?.trim().slice(0, 20) ?? ''
        };
      });

    for (let press = 0; press < 10; press += 1) {
      await page.keyboard.press('Tab');

      const where = await focused();

      expect(where.inside || where.tag === 'BODY', `focus escaped to ${where.name}`).toBe(true);
    }
  });

  test('gives focus back to the control that opened it', async ({ page }) => {
    const trigger = page.getByRole('button', { name: 'Open modal' });

    // Safari leaves pointer-activated buttons unfocused. Exercise keyboard focus restoration.
    await trigger.focus();
    await trigger.press('Enter');
    await page.getByRole('button', { name: 'Close' }).click();

    await expect(page.getByRole('dialog')).toBeHidden();
    await expect(trigger).toBeFocused();
  });

  test('closes on Escape', async ({ page }) => {
    await page.getByRole('button', { name: 'Open modal' }).click();
    await page.keyboard.press('Escape');

    await expect(page.getByRole('dialog')).toBeHidden();
  });

  test('closes when the backdrop is clicked, which is not the only way out', async ({ page }) => {
    await page.getByRole('button', { name: 'Open sheet' }).click();

    const dialog = page.getByRole('dialog');

    await expect(dialog).toBeVisible();
    await page.mouse.click(5, 5);

    await expect(dialog).toBeHidden();
  });

  test('undoes a destructive action from the toast, instead of asking first', async ({ page }) => {
    await page.getByRole('button', { name: 'Reset' }).click();
    await expect(page.getByTestId('undo-state')).toHaveText('deleted');

    await page.getByRole('button', { name: 'Delete with undo' }).click();
    const undo = page.getByRole('button', { name: 'Undo', exact: true });

    await undo.click();

    await expect(page.getByTestId('undo-state')).toHaveText('restored');
    await expect(undo).toBeHidden();
  });

  test('moves between tabs with the arrow keys', async ({ page }) => {
    await page.getByRole('tab', { name: 'Ingredients' }).focus();
    await page.keyboard.press('ArrowRight');

    await expect(page.getByRole('tab', { name: 'Steps' })).toBeFocused();
    await expect(page.getByTestId('tab-panel')).toHaveText('steps');

    await page.keyboard.press('End');

    await expect(page.getByRole('tab', { name: 'Notes' })).toBeFocused();
  });
});
