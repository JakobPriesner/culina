import { fileURLToPath } from 'node:url';
import AxeBuilder from '@axe-core/playwright';
import { expect, test, type Page } from '@playwright/test';
import { expectReflow, recipeId, responsiveData } from './support/responsive';

async function libraryData(page: Page, locale: 'en' | 'de' = 'en', mode = 'light') {
  await responsiveData(page, locale);
  await page.route('**/api/v1/users/me/settings', (route) =>
    route.fulfill({
      json: {
        locale,
        theme: 'warm-paper',
        mode,
        measurementSystem: 'metric',
        version: 1
      }
    })
  );
  await page.route('**/api/v1/cook-sessions/current', (route) => route.fulfill({ status: 204 }));
  await page.route('**/api/v1/recipes/recipe-1/image?*', (route) =>
    route.fulfill({
      path: fileURLToPath(new URL('../../static/images/culina-tomato-toast.webp', import.meta.url)),
      contentType: 'image/webp'
    })
  );
  await page.route('**/api/v1/recipes?*', (route) => {
    const url = new URL(route.request().url());
    const query = (url.searchParams.get('query') ?? '').toLowerCase();
    const maxMinutes = Number(url.searchParams.get('maxMinutes') ?? Infinity);
    const items = [
      {
        title: 'Lemon orzo with summer greens',
        totalMinutes: 25,
        imageId: 'photo',
        tags: ['Weeknight favourites']
      },
      {
        title: 'Tomato toast with fresh basil',
        totalMinutes: 15,
        imageId: 'photo',
        tags: ['Simple pleasures']
      },
      {
        title: 'Slow-roasted seasonal vegetables',
        totalMinutes: 60,
        imageId: null,
        tags: ['From the oven']
      },
      {
        title: 'Sunday morning pancakes',
        totalMinutes: 30,
        imageId: null,
        tags: ['Weekend rituals']
      }
    ]
      .map((recipe, index) => ({
        ...recipe,
        recipeId: index === 0 ? recipeId : `recipe-${index}`,
        yieldAmount: 2,
        yieldKind: 'servings',
        cookCount: index,
        updatedAt: '2026-09-14T12:00:00Z'
      }))
      .filter(
        (recipe) => recipe.title.toLowerCase().includes(query) && recipe.totalMinutes <= maxMinutes
      );
    return route.fulfill({ json: { items, total: items.length, nextCursor: null } });
  });
}

test.describe('library refinement @offline', () => {
  test.use({ serviceWorkers: 'block' });

  test('search and quick filter survive opening a recipe and returning', async ({ page }) => {
    await libraryData(page);
    await page.goto('/');
    const search = page.getByRole('searchbox', { name: 'Search recipes' });
    await search.fill('orzo');
    await page.getByRole('button', { name: 'Up to 30 min' }).click();
    await expect(page.locator('.count')).toHaveText('1 recipe');
    await page.getByRole('link', { name: 'Open Lemon orzo with summer greens' }).click();
    await page.getByRole('link', { name: /All recipes/i }).click();
    await expect(search).toHaveValue('orzo');
    await expect(page.getByRole('button', { name: 'Up to 30 min' })).toHaveAttribute(
      'aria-pressed',
      'true'
    );
    await expect(page.locator('.count')).toHaveText('1 recipe');
    await page.getByRole('button', { name: 'Show all recipes' }).click();
    await expect(search).toHaveValue('');
    await expect(page.locator('.count')).toHaveText('4 recipes');
    await page.getByRole('button', { name: 'Up to 30 min' }).click();
    await expect(page.locator('.count')).toHaveText('3 recipes');
    await search.fill('something absent');
    await expect(page.getByRole('heading', { name: 'Nothing matched' })).toBeVisible();
    await search.press('Escape');
    await expect(search).toBeFocused();
    await expect(page.locator('.count')).toHaveText('3 recipes');
  });

  test('collection destinations preserve search and keep recipe creation direct', async ({
    page
  }, testInfo) => {
    const desktop = testInfo.project.name === 'desktop';
    await page.setViewportSize({ width: desktop ? 1280 : 390, height: 740 });
    await libraryData(page);
    await page.goto('/');
    const navigation = page.getByRole('navigation', {
      name: 'Recipe book'
    });
    const search = page.getByRole('searchbox', { name: 'Search recipes' });
    await search.fill('orzo');
    await expect(page.locator('.count')).toHaveText('1 recipe');
    if (!desktop) await page.getByRole('button', { name: 'Recipe book', exact: true }).click();
    await navigation.getByRole('link', { name: 'Cookbooks', exact: true }).click();
    await expect(page).toHaveURL(/\/cookbooks$/);
    if (!desktop) {
      await expect(page.getByRole('dialog')).not.toBeVisible();
      await page.getByRole('button', { name: 'Recipe book', exact: true }).click();
    }
    await expect(navigation.getByRole('link', { name: 'Cookbooks', exact: true })).toHaveAttribute(
      'aria-current',
      'page'
    );
    await expect(navigation.locator('[aria-current="page"]')).toHaveCount(1);
    await navigation.getByRole('link', { name: 'All recipes', exact: true }).click();
    await expect(search).toHaveValue('orzo');
    await expect(page.locator('.count')).toHaveText('1 recipe');
    await search.press('Escape');
    await expect(page.locator('.count')).toHaveText('4 recipes');
    await page.getByRole('link', { name: 'Open Sunday morning pancakes' }).scrollIntoViewIfNeeded();
    if (desktop) {
      await expect(
        navigation.getByRole('link', { name: 'Cookbooks', exact: true })
      ).toBeInViewport();
    } else {
      await expect(page.getByRole('button', { name: 'Recipe book', exact: true })).toBeInViewport();
    }
    const create = page.getByRole('link', { name: 'Add a recipe', exact: true });
    await expect(create).toBeInViewport({ ratio: 1 });
    await create.click();
    await expect(page).toHaveURL(/\/recipes\/new$/);
    await expect(page.getByRole('heading', { name: 'New recipe', exact: true })).toBeVisible();
  });

  test('compact library sheet dismisses, restores focus, and adapts to desktop', async ({
    page
  }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop', 'Explicit viewport matrix.');
    await libraryData(page);
    await page.setViewportSize({ width: 320, height: 740 });
    await page.goto('/shopping');
    const launcher = page.getByRole('button', { name: 'Recipe book', exact: true });
    const sheet = page.getByRole('dialog', { name: 'Recipe book' });
    const appBar = page.getByRole('navigation', { name: 'Sections' });
    await expect(appBar.getByRole('link')).toHaveCount(2);
    await expect(appBar.getByRole('button')).toHaveCount(1);
    await launcher.click();
    await expect(sheet).toBeVisible();
    await expect(launcher).toHaveAttribute('aria-expanded', 'true');
    await page.keyboard.press('Escape');
    await expect(sheet).not.toBeVisible();
    await expect(launcher).toBeFocused();
    await expect(launcher).toHaveAttribute('aria-expanded', 'false');
    await launcher.click();
    await sheet.getByRole('button', { name: 'Close', exact: true }).click();
    await expect(launcher).toBeFocused();
    await launcher.click();
    await sheet.getByRole('link', { name: 'Cookbooks', exact: true }).click();
    await expect(page).toHaveURL(/\/cookbooks$/);
    await expect(sheet).not.toBeVisible();
    await launcher.click();
    await expect(sheet.getByRole('link', { name: 'Cookbooks', exact: true })).toHaveAttribute(
      'aria-current',
      'page'
    );
    await page.setViewportSize({ width: 1280, height: 900 });
    await expect(sheet).not.toBeVisible();
    await expect(page.getByRole('navigation', { name: 'Recipe book' })).toBeVisible();
    await expectReflow(page);
  });

  test('planning remains reachable from collection navigation', async ({ page }, testInfo) => {
    const desktop = testInfo.project.name === 'desktop';
    await page.setViewportSize({ width: desktop ? 1280 : 390, height: 900 });
    await libraryData(page);
    await page.goto('/');
    if (!desktop) await page.getByRole('button', { name: 'Recipe book', exact: true }).click();
    await page
      .getByRole('navigation', { name: 'Recipe book' })
      .getByRole('link', { name: 'Plan the week', exact: true })
      .click();
    await expect(page).toHaveURL(/\/plan$/);
    await expect(page.getByRole('heading', { name: 'This week', exact: true })).toBeVisible();
    await expect(page.getByRole('dialog')).not.toBeVisible();
  });

  for (const locale of ['en', 'de'] as const) {
    for (const mode of ['light', 'dark']) {
      test(`${locale} ${mode} library reflows and remains accessible`, async ({
        page
      }, testInfo) => {
        test.skip(testInfo.project.name !== 'desktop', 'Explicit viewport matrix.');
        await libraryData(page, locale, mode);
        await page.emulateMedia({ reducedMotion: 'reduce' });
        for (const width of [320, 390, 768, 1280]) {
          await page.setViewportSize({ width, height: 900 });
          await page.goto('/');
          await expect(page.locator('.count')).toHaveText(
            locale === 'en' ? '4 recipes' : '4 Rezepte'
          );
          await expectReflow(page);
          const results = await new AxeBuilder({ page })
            .withTags(['wcag2a', 'wcag2aa', 'wcag21aa'])
            .analyze();
          expect(results.violations).toEqual([]);
          if (width < 1024) {
            const launcher = page.getByRole('button', {
              name: locale === 'en' ? 'Recipe book' : 'Rezeptbuch',
              exact: true
            });
            await launcher.click();
            const sheet = page.getByRole('dialog');
            await expect(sheet).toBeVisible();
            await expectReflow(page);
            expect(
              (await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa']).analyze())
                .violations
            ).toEqual([]);
            if (width === 320)
              await page.screenshot({
                path: testInfo.outputPath(`library-sheet-${locale}-${mode}.png`)
              });
            await sheet
              .getByRole('button', { name: locale === 'en' ? 'Close' : 'Schließen', exact: true })
              .click();
          }
          if (locale === 'en' && [390, 1280].includes(width)) {
            await page.screenshot({
              path: testInfo.outputPath(`library-${mode}-${width}.png`),
              fullPage: true
            });
          }
        }
      });
    }
  }
});
