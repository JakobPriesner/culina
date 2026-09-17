import { fileURLToPath } from 'node:url';
import AxeBuilder from '@axe-core/playwright';
import { expect, test, type Page } from '@playwright/test';
import { cookbookId, expectReflow, recipeId, responsiveData } from './support/responsive';

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
      name: 'Sections'
    });
    const search = page.getByRole('searchbox', { name: 'Search recipes' });
    await search.fill('orzo');
    await expect(page.locator('.count')).toHaveText('1 recipe');
    await navigation.getByRole('link', { name: 'Cookbooks', exact: true }).click();
    await expect(page).toHaveURL(/\/cookbooks$/);
    await expect(navigation.getByRole('link', { name: 'Cookbooks', exact: true })).toHaveAttribute(
      'aria-current',
      'page'
    );
    await expect(navigation.locator('[aria-current="page"]')).toHaveCount(1);
    await navigation.getByRole('link', { name: 'Recipes', exact: true }).click();
    await expect(search).toHaveValue('orzo');
    await expect(page.locator('.count')).toHaveText('1 recipe');
    await search.press('Escape');
    await expect(page.locator('.count')).toHaveText('4 recipes');
    await page.getByRole('link', { name: 'Open Sunday morning pancakes' }).scrollIntoViewIfNeeded();
    await expect(navigation.getByRole('link', { name: 'Cookbooks', exact: true })).toBeInViewport();
    const create = page.getByRole('link', { name: 'Add a recipe', exact: true });
    await expect(create).toBeInViewport({ ratio: 1 });
    await create.click();
    await expect(page).toHaveURL(/\/recipes\/new$/);
    await expect(page.getByRole('heading', { name: 'New recipe', exact: true })).toBeVisible();
  });

  test('every destination is one tap away and keeps its selected state', async ({
    page
  }, testInfo) => {
    await libraryData(page);
    await page.setViewportSize({
      width: testInfo.project.name === 'desktop' ? 1280 : 320,
      height: 900
    });
    await page.goto('/');
    const navigation = page.getByRole('navigation', { name: 'Sections' });
    const expected = [
      ['Recipes', '/'],
      ['Cookbooks', '/cookbooks'],
      ['Week', '/plan'],
      ['Shopping', '/shopping'],
      ['Me', '/me']
    ] as const;
    await expect(navigation.getByRole('link')).toHaveCount(5);
    await expect(navigation.getByRole('button')).toHaveCount(0);
    for (const [label, path] of expected) {
      const link = navigation.getByRole('link', { name: label, exact: true });
      await expect(link).toBeInViewport({ ratio: 1 });
      await link.click();
      await expect(page).toHaveURL(new RegExp(`${path === '/' ? '/' : path}$`));
      await expect(link).toHaveAttribute('aria-current', 'page');
      await expect(navigation.locator('[aria-current="page"]')).toHaveCount(1);
      await expect(page.getByRole('dialog')).not.toBeVisible();
    }
    await page.goBack();
    await expect(navigation.getByRole('link', { name: 'Shopping', exact: true })).toHaveAttribute(
      'aria-current',
      'page'
    );
    await page.goto(`/cookbooks/${cookbookId}`);
    await expect(navigation.getByRole('link', { name: 'Cookbooks', exact: true })).toHaveAttribute(
      'aria-current',
      'page'
    );
    await expect(page.getByRole('navigation')).toHaveCount(1);
    await expectReflow(page);
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
          const navigation = page.getByRole('navigation', {
            name: locale === 'en' ? 'Sections' : 'Bereiche'
          });
          await expect(navigation.getByRole('link')).toHaveCount(5);
          for (const link of await navigation.getByRole('link').all()) {
            await expect(link).toBeInViewport({ ratio: 1 });
            const label = link.locator('.label');
            const fitsOneLine = await label.evaluate(
              (element) =>
                element.getBoundingClientRect().height <=
                parseFloat(getComputedStyle(element).lineHeight) + 1
            );
            expect(fitsOneLine, `Navigation label at ${width}px`).toBe(true);
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
