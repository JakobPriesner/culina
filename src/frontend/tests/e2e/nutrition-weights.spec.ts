import AxeBuilder from '@axe-core/playwright';
import { expect, test, type Page } from '@playwright/test';

import { expectReflow, recipeId, responsiveData } from './support/responsive';

/* The routed fixture stands in for the backend, so these need nothing but the built app. */
test.describe('typical weights, household weights and kinds of a food @offline', () => {
  test.use({ serviceWorkers: 'block' });

  const open = async (page: Page, locale: 'de' | 'en' = 'de') => {
    await responsiveData(page, locale, { nutritionLines: true, estimates: true });
    // Settled, not mid-fade: axe would otherwise measure the sheet's text while it is still fading in.
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await page.goto(`/recipes/${recipeId}`);
    await page.locator('#nutrition summary').click();
  };

  const energy = async (page: Page) =>
    Number(
      await page
        .locator('#nutrition .energyNumber')
        .innerText()
        .then((t) => t.replace(/\D/g, ''))
    );

  const row = (page: Page, name: string) =>
    page.locator('#nutrition li.row', { hasText: name }).first();

  test('names what was estimated, and reads the row as an estimate with its source', async ({
    page
  }) => {
    await open(page);

    await expect(page.locator('#nutrition .summaryOverview')).toContainText(
      'teils geschätzt: Zwiebel'
    );
    await expect(row(page, 'Zwiebel')).toContainText('≈ 110 g · typisch');
    await expect(row(page, 'Zwiebel')).toContainText('Quelle: USDA FoodData Central');
    await expect(page.getByRole('row', { name: /Brennwert/ })).toContainText('≈');
  });

  test('chooses another kind of milk in one tap and the figure follows', async ({ page }) => {
    await open(page);

    const before = await energy(page);

    await page.getByRole('button', { name: 'Was ist „Milch“? Ändern' }).click();

    const kinds = page.getByRole('list', { name: 'Sorte' });

    await expect(kinds.getByRole('button', { name: /Vollmilch 3,5 %/ })).toHaveAttribute(
      'aria-current',
      'true'
    );
    await expect(
      kinds.getByRole('button', { name: /fettarme Milch 1,5 %\s*47 kcal/ })
    ).toBeVisible();
    await kinds.getByRole('button', { name: /fettarme Milch 1,5 %/ }).click();

    await expect(row(page, 'Milch')).toContainText('als fettarme Milch 1,5 %');
    await expect.poll(() => energy(page)).toBeLessThan(before);
  });

  test('says what one onion weighs, and the row, headline and settings follow', async ({
    page
  }) => {
    await open(page);

    await page.getByRole('button', { name: 'Was ist „Zwiebel“? Ändern' }).click();

    const field = page.getByRole('textbox', { name: 'Gewicht in Gramm' });

    await expect(
      page.getByRole('heading', { name: 'Wie viel wiegt 1 Stück „Zwiebel“?' })
    ).toBeVisible();
    await expect(field).toHaveValue('110');
    await expect(page.getByText('typisch 110 g · USDA FoodData Central')).toBeVisible();

    const axe = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
      .analyze();

    expect(axe.violations.map((violation) => violation.id)).toEqual([]);

    await field.fill('150');
    await page.getByRole('button', { name: 'Speichern' }).click();

    await expect(row(page, 'Zwiebel')).toContainText('150 g · euer Wert');
    await expect(page.locator('#nutrition .summaryOverview')).not.toContainText('geschätzt');

    await page.goto('/me/household');

    const facts = page.getByRole('list', { name: 'Zutaten' });

    await expect(facts).toContainText('1 Stück = 150 g');

    const result = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
      .analyze();

    expect(result.violations.map((violation) => violation.id)).toEqual([]);

    await page.getByRole('button', { name: /„zwiebel“: 1 Stück = 150 g entfernen/ }).click();
    await expect(page.getByText('Du kannst Zutaten in der Nährwert-Aufschlüsselung')).toBeVisible();
  });

  test('turning typical weights off sends the onion back to not counted', async ({ page }) => {
    await responsiveData(page, 'de', { nutritionLines: true, estimates: true });
    await page.goto('/me/household');

    const toggle = page.getByRole('switch', { name: 'Typische Gewichte verwenden' });

    await expect(toggle).toBeEnabled();
    await expect(toggle).toBeChecked();
    await toggle.click();
    await expect(toggle).not.toBeChecked();

    await page.goto(`/recipes/${recipeId}`);
    await page.locator('#nutrition summary').click();

    await expect(page.locator('#nutrition .summaryOverview')).not.toContainText('geschätzt');
    await expect(row(page, 'Zwiebel')).toContainText('Stückzahl, kein Gewicht');
    await expect(
      row(page, 'Zwiebel').getByRole('button', { name: /Gewicht angeben/ })
    ).toBeVisible();
  });

  test('does not spill past a 320 px screen, sheet and settings included', async ({
    page
  }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop', 'Explicit viewport.');
    await page.setViewportSize({ width: 320, height: 720 });
    await open(page);
    await expectReflow(page);
    await page.getByRole('button', { name: 'Was ist „Milch“? Ändern' }).click();
    await expect(page.getByRole('list', { name: 'Sorte' })).toBeVisible();
    await expectReflow(page);
    await page.keyboard.press('Escape');
    await page.getByRole('button', { name: 'Was ist „Zwiebel“? Ändern' }).click();
    await expect(page.getByRole('textbox', { name: 'Gewicht in Gramm' })).toBeVisible();
    await expectReflow(page);
    await page.keyboard.press('Escape');
    await page.goto('/me/household');
    await expect(page.getByRole('switch', { name: 'Typische Gewichte verwenden' })).toBeVisible();
    await expectReflow(page);
  });
});
