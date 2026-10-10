import AxeBuilder from '@axe-core/playwright';
import { expect, test, type Page } from '@playwright/test';

import { expectReflow, recipeId, responsiveData } from './support/responsive';

/* The routed fixture stands in for the backend, so these need nothing but the built app. */
test.describe('nutrition on a recipe @offline', () => {
  // A service worker would answer these reads itself, past the route.
  test.use({ serviceWorkers: 'block' });

  const words = {
    de: {
      headline: /mind\.\s520\skcal\spro\sPortion\s·\s3\svon\s5\sZutaten/,
      energy: /mind\.\s2\.179\skJ\s\/\s520\skcal/,
      counted: 'Gezählt',
      notCounted: 'Nicht gezählt',
      reason: 'Menge nicht in Gramm',
      byDensity: /≈\s27\sg\s·\süber die Dichte/,
      credit: 'Nährwerte: Max Rubner-Institut, Bundeslebensmittelschlüssel 4.0 (CC BY 4.0)'
    },
    en: {
      headline: /at\sleast\s520\skcal\sper\sserving\s·\s3\sof\s5\singredients/,
      energy: /at\sleast\s2,179\skJ\s\/\s520\skcal/,
      counted: 'Counted',
      notCounted: 'Not counted',
      reason: 'amount not in grams',
      byDensity: /≈\s27\sg\s·\sby density/,
      credit: 'Nutrition values: Max Rubner-Institut, Bundeslebensmittelschlüssel 4.0 (CC BY 4.0)'
    }
  } as const;

  for (const locale of ['de', 'en'] as const) {
    test(`says what it covers and shows how it was counted in ${locale}`, async ({ page }) => {
      const said = words[locale];

      await responsiveData(page, locale, { nutritionLines: true });
      await page.goto(`/recipes/${recipeId}`);

      const summary = page.locator('summary', { hasText: said.headline });

      await expect(summary).toBeVisible();
      await expect(page.getByRole('table')).toBeHidden();

      await summary.click();

      const table = page.getByRole('table');

      await expect(table.getByRole('row', { name: said.energy })).toBeVisible();
      await expect(page.getByRole('heading', { name: said.counted, exact: true })).toBeVisible();
      await expect(page.getByRole('heading', { name: said.notCounted, exact: true })).toBeVisible();
      await expect(page.getByText(said.reason)).toBeVisible();
      await expect(page.getByText(said.byDensity)).toBeVisible();
      await expect(page.locator('p.source:visible', { hasText: said.credit })).toBeVisible();
      await expect(
        page.getByRole('link', { name: /Bundeslebensmittelschlüssel 4\.0/ })
      ).toHaveAttribute('href', 'https://blsdb.de');
    });
  }

  test('is reached by Tab and opens with Enter', async ({ page, isMobile }) => {
    test.skip(isMobile, 'No keyboard on a phone.');
    await responsiveData(page, 'en', { nutritionLines: true });
    await page.goto(`/recipes/${recipeId}`);

    const summary = page.locator('summary', { hasText: 'Nutrition' });

    await expect(summary).toBeVisible();

    for (let presses = 0; presses < 80; presses += 1) {
      await page.keyboard.press('Tab');

      if (await summary.evaluate((element) => element === document.activeElement)) {
        break;
      }
    }

    await expect(summary).toBeFocused();
    await page.keyboard.press('Enter');
    await expect(page.getByRole('table')).toBeVisible();
    await page.keyboard.press('Enter');
    await expect(page.getByRole('table')).toBeHidden();
  });

  test('does not spill past a 320 px screen, closed or open', async ({ page }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop', 'Explicit viewport.');
    await page.setViewportSize({ width: 320, height: 720 });
    await responsiveData(page, 'de', { extraIngredients: 3, nutritionLines: true });
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await page.goto(`/recipes/${recipeId}`);

    const summary = page.locator('summary', { hasText: 'Nährwerte' });

    await expect(summary).toBeVisible();
    await expectReflow(page);
    await summary.click();
    await expect(page.getByRole('table')).toBeVisible();
    await expectReflow(page);
  });

  for (const mode of ['light', 'dark'] as const) {
    test(`is accessible when open, in ${mode} mode`, async ({ browser }) => {
      // Explicit context: axe refuses a page made straight from the browser.
      const context = await browser.newContext({
        reducedMotion: 'reduce',
        serviceWorkers: 'block'
      });
      const page = await context.newPage();

      await page.addInitScript((appearance) => {
        localStorage.setItem(
          'culina.appearance',
          JSON.stringify({ theme: 'warm-paper', mode: appearance })
        );
      }, mode);
      await responsiveData(page, 'de', { nutritionLines: true });
      await page.goto(`/recipes/${recipeId}`);
      await page.locator('summary', { hasText: 'Nährwerte' }).click();
      await expect(page.getByRole('table')).toBeVisible();

      const result = await new AxeBuilder({ page })
        .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
        .analyze();

      expect(result.violations.map((violation) => violation.id)).toEqual([]);
      await context.close();
    });
  }

  test('prints the headline and the credit with the panel closed, and no chevron', async ({
    page
  }) => {
    await responsiveData(page, 'de', { nutritionLines: true });
    await page.goto(`/recipes/${recipeId}`);
    await expect(page.locator('summary', { hasText: 'Nährwerte' })).toBeVisible();
    await page.emulateMedia({ media: 'print' });

    await expect(page.locator('summary', { hasText: /mind\.\s520\skcal/ })).toBeVisible();
    await expect(page.locator('p.source:visible')).toHaveCount(1);
    await expect(page.locator('p.source:visible')).toContainText('Max Rubner-Institut');
    await expect(page.locator('summary .chevron')).toBeHidden();
    await expect(page.getByRole('table')).toBeHidden();
  });

  test.describe('saying what an ingredient is', () => {
    const open = async (page: Page, options: { refuseCorrections?: boolean } = {}) => {
      await responsiveData(page, 'en', { nutritionLines: true, ...options });
      await page.goto(`/recipes/${recipeId}`);
      await page.locator('summary', { hasText: 'Nutrition' }).click();
      await page
        .getByRole('button', {
          name: 'What is “Olivenöl”? Change'
        })
        .click();
    };

    test('states its reach, corrects the line and shows the new figure', async ({ page }) => {
      await open(page);

      const sheet = page.getByRole('dialog', { name: 'What is “Olivenöl”?' });

      await expect(sheet).toBeVisible();
      await expect(
        sheet.getByText('Applies to every recipe of yours that uses “Olivenöl”.')
      ).toBeVisible();
      await expect(sheet.getByRole('button', { name: /Olive oil\s*Default/ })).toHaveAttribute(
        'aria-current',
        'true'
      );
      await expect(sheet.getByRole('button', { name: 'Back to the default' })).toBeHidden();

      await sheet.getByRole('button', { name: /Rapeseed oil\s*828 kcal per 100 g/ }).click();

      await expect(sheet).toBeHidden();
      await expect(page.getByText('“Olivenöl” now counts as Rapeseed oil.')).toBeVisible();
      await expect(page.locator('.detail', { hasText: 'as Rapeseed oil' })).toBeVisible();
      await expect(page.locator('summary', { hasText: /560 kcal/ })).toBeVisible();

      // Reopened, it is the household's choice, and it can be given back.
      await page.getByRole('button', { name: 'What is “Olivenöl”? Change' }).click();
      await expect(
        page.getByRole('button', { name: /Rapeseed oil\s*your choice/ })
      ).toHaveAttribute('aria-current', 'true');
      await page.getByRole('button', { name: 'Back to the default' }).click();
      await expect(page.getByText('“Olivenöl” counts as the default again.')).toBeVisible();
      await expect(page.locator('summary', { hasText: /520 kcal/ })).toBeVisible();
    });

    test('moves a line to not counted', async ({ page }) => {
      await open(page);
      await page.getByRole('button', { name: "Don't count this" }).click();

      await expect(page.getByText('“Olivenöl” is no longer counted.')).toBeVisible();
      // The line moved to another group; the keyboard user is still on its button.
      await expect(page.getByRole('button', { name: 'What is “Olivenöl”? Change' })).toBeFocused();
      await expect(page.getByText('excluded by your household')).toBeVisible();
    });

    test('puts the line back and says why when the server refuses', async ({ page }) => {
      await open(page, { refuseCorrections: true });
      await page.getByRole('button', { name: /Rapeseed oil/ }).click();

      await expect(
        page.getByText('That food is not in the nutrition table. Pick another one.')
      ).toBeVisible();
      await expect(page.locator('.detail', { hasText: 'as Olive oil' })).toBeVisible();
      await expect(page.locator('.detail', { hasText: 'as Rapeseed oil' })).toBeHidden();
      await expect(page.locator('summary', { hasText: /520 kcal/ })).toBeVisible();
    });
  });
});
