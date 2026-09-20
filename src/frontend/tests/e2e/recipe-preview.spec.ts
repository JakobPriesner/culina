import AxeBuilder from '@axe-core/playwright';
import { expect, test, type Page } from '@playwright/test';

/** Waits for the two animation frames a style change needs to become visible. */
const settle = (page: Page) =>
  page.evaluate(
    () =>
      new Promise<void>((resolve) =>
        requestAnimationFrame(() => requestAnimationFrame(() => resolve()))
      )
  );

test.describe('recipe experience preview @offline', () => {
  test.beforeEach(async ({ page }) => {
    await page.addInitScript(() => localStorage.setItem('culina.locale', 'en'));
    await page.goto('/design/recipes');
    await expect(page.getByRole('heading', { name: 'Your recipe book.' })).toBeVisible();
  });

  test('filters, finds ingredients and recovers from empty results', async ({ page }) => {
    await page.getByRole('button', { name: 'Up to 30 min', exact: true }).click();
    await expect(page.getByRole('status')).toHaveText('2 recipes');
    const search = page.getByRole('searchbox');
    await search.fill('tomatoes');
    await expect(page.getByRole('status')).toHaveText('1 recipe');
    await search.fill('unfindable');
    await expect(page.getByRole('heading', { name: 'Nothing here just yet' })).toBeVisible();
    await page.getByRole('button', { name: 'Show all recipes', exact: true }).click();
    await expect(page.getByRole('status')).toHaveText('3 recipes');
  });

  test('keeps favourites independent from opening a recipe', async ({ page }) => {
    await page
      .getByRole('button', { name: 'Favourite: Tomatoes on sourdough', exact: true })
      .click();
    await page.getByRole('button', { name: 'Favourites', exact: true }).click();
    await expect(page.getByRole('status')).toHaveText('2 recipes');
    await page.getByRole('button', { name: 'Tomatoes on sourdough', exact: true }).click();
    await expect(page.getByRole('heading', { level: 1 })).toHaveText('Tomatoes on sourdough');
  });

  test('scales, checks ingredients and keeps their state through cooking', async ({ page }) => {
    await page.getByRole('button', { name: 'View recipe', exact: true }).click();
    await page.getByRole('button', { name: 'One more serving', exact: true }).click();
    await expect(page.getByRole('spinbutton', { name: 'Servings' })).toHaveValue('3');
    await expect(page.getByText('300 g', { exact: true })).toBeVisible();
    await page.getByRole('checkbox', { name: 'Orzo', exact: true }).check();
    await page.getByRole('button', { name: 'Start cooking', exact: true }).click();
    await expect(page.getByText('Step 1 of 3', { exact: true })).toBeVisible();
    await page.getByRole('button', { name: 'Next step', exact: true }).click();
    await expect(page.getByText('Step 2 of 3', { exact: true })).toBeVisible();
    await page.getByRole('button', { name: 'Back to recipe', exact: true }).click();
    await expect(page.getByRole('checkbox', { name: 'Orzo', exact: true })).toBeChecked();
    await expect(page.getByRole('spinbutton', { name: 'Servings' })).toHaveValue('3');
  });
});

for (const mode of ['light', 'dark']) {
  test(`German preview is accessible in ${mode} mode @offline`, async ({ page, isMobile }) => {
    await page.addInitScript((mode) => {
      localStorage.setItem('culina.locale', 'de');
      localStorage.setItem('culina.appearance', JSON.stringify({ theme: 'warm-paper', mode }));
    }, mode);
    if (isMobile) await page.setViewportSize({ width: 320, height: 720 });
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await page.goto('/design/recipes');
    await expect(
      page.getByRole('heading', { name: 'Dein Rezeptbuch.', exact: true })
    ).toBeVisible();

    const check = async () => {
      // The colour of a control that has just changed variant is still the old
      // one for a single frame, transitions or no transitions. Axe reads the
      // computed value, so it has to look after that frame, not during it.
      await settle(page);
      const result = await new AxeBuilder({ page })
        .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
        .analyze();
      expect(result.violations).toEqual([]);
      expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBe(
        page.viewportSize()!.width
      );
    };
    await check();
    await page.getByRole('button', { name: 'Rezept ansehen', exact: true }).click();
    await check();
    await page.getByRole('button', { name: 'Kochen starten', exact: true }).click();
    await check();
    if (isMobile) {
      await page.getByRole('button', { name: 'Zutaten', exact: true }).click();
      await expect(page.getByRole('dialog', { name: 'Zutaten', exact: true })).toBeVisible();
      await check();
    }
  });
}

test.describe('recipe preview continuity @offline', () => {
  test.beforeEach(async ({ page }) => {
    await page.addInitScript(() => localStorage.setItem('culina.locale', 'en'));
    await page.goto('/design/recipes');
  });

  test('returns to the same collection and resumes the current cooking step', async ({ page }) => {
    await page.getByRole('searchbox').fill('orzo');
    const opener = page.getByRole('button', {
      name: 'Lemon orzo with golden zucchini',
      exact: true
    });
    await opener.click();
    await page.getByRole('button', { name: 'One more serving', exact: true }).click();
    await page.getByRole('checkbox', { name: 'Orzo', exact: true }).check();
    await page.getByRole('button', { name: 'Start cooking', exact: true }).click();
    await page.getByRole('button', { name: 'Next step', exact: true }).click();
    await expect(page.locator('[aria-current="step"]')).toBeFocused();
    await page.getByRole('button', { name: '← Recipe book', exact: true }).click();
    await expect(opener).toBeFocused();
    await expect(page.getByRole('searchbox')).toHaveValue('orzo');
    await expect(page.getByRole('status')).toHaveText('1 recipe');
    await page.getByRole('button', { name: 'Continue cooking', exact: true }).click();
    await expect(page.getByText('Step 2 of 3', { exact: true })).toBeVisible();
    await expect(page.locator('[aria-current="step"]')).toBeFocused();
    await page.getByRole('button', { name: 'Back to recipe', exact: true }).click();
    await expect(page.getByRole('spinbutton', { name: 'Servings' })).toHaveValue('3');
    await expect(page.getByRole('checkbox', { name: 'Orzo', exact: true })).toBeChecked();
  });

  test('keeps mobile ingredients and step controls reachable at 320px', async ({ page }) => {
    await page.setViewportSize({ width: 320, height: 720 });
    await page.getByRole('button', { name: 'View recipe', exact: true }).click();
    await page.getByRole('button', { name: 'Start cooking', exact: true }).click();
    await expect(page.locator('[aria-current="step"]')).toBeInViewport();
    const ingredients = page.getByRole('button', { name: 'Ingredients', exact: true });
    await expect(ingredients).toBeInViewport();
    await ingredients.click();
    const sheet = page.getByRole('dialog', { name: 'Ingredients', exact: true });
    await expect(sheet).toBeVisible();
    await sheet.getByRole('checkbox', { name: 'Orzo', exact: true }).check();
    await sheet.getByRole('button', { name: 'One more serving', exact: true }).click();
    await page.getByRole('button', { name: 'Close ingredients', exact: true }).click();
    await expect(ingredients).toBeFocused();
    await page.getByRole('button', { name: 'Next step', exact: true }).click();
    await expect(page.getByText('Step 2 of 3', { exact: true })).toBeVisible();
    await expect(page.locator('[aria-current="step"]')).toBeInViewport();
    await expect(page.getByRole('button', { name: 'Next step', exact: true })).toBeInViewport({
      ratio: 1
    });
    expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBe(320);
    await page.getByRole('button', { name: 'Back to recipe', exact: true }).click();
    await expect(page.getByRole('checkbox', { name: 'Orzo', exact: true })).toBeChecked();
    await expect(page.getByRole('spinbutton', { name: 'Servings' })).toHaveValue('3');
  });
});
