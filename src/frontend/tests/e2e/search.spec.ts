import { expect, test } from '@playwright/test';
import { recipeId, responsiveData } from './support/responsive';

/**
 * The search overlay, over the pages it opens on.
 *
 * Against fixtures rather than a server: what is being proven is the
 * interface's half of the contract — that a reading comes back as a chip, that
 * removing it removes exactly its characters from what is asked next, and that
 * the keyboard reaches a recipe without the field ever losing focus. The
 * server's half is proven in RecipeSearchRelevanceTests.
 */
test.describe('search @offline', () => {
  test.use({ serviceWorkers: 'block' });

  test('reads a sentence into chips, and removing one asks again without it', async ({
    page
  }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop', 'One keyboard walk is enough.');
    await responsiveData(page);
    const asked: string[] = [];

    await page.route('**/api/v1/households/*/completions**', (route) =>
      route.fulfill({ json: { items: [] } })
    );
    await page.route('**/api/v1/recipes?**', async (route) => {
      const query = new URL(route.request().url()).searchParams.get('query');

      if (query === null) {
        return route.fallback();
      }

      asked.push(query);
      const vegetarian = query.includes('vegetarisch');

      return route.fulfill({
        json: {
          items: [
            {
              recipeId,
              title: 'Sonnenblumenkernvollkornbrot mit geröstetem Sommergemüse',
              imageId: null,
              totalMinutes: 35,
              yieldAmount: 2,
              yieldKind: 'servings',
              tags: [],
              cookCount: 0,
              updatedAt: '2026-09-14T12:00:00Z',
              matchReason: { kind: 'ingredient', term: 'Sommergemüse' }
            }
          ],
          total: 1,
          interpretation: {
            freeText: '',
            applied: [
              ...(vegetarian
                ? [{ kind: 'diet', value: 'vegetarian', text: 'vegetarisch', start: 0, end: 11 }]
                : []),
              {
                kind: 'time',
                value: '30',
                text: 'unter 30 Minuten',
                start: query.indexOf('unter'),
                end: query.indexOf('unter') + 16
              }
            ]
          }
        }
      });
    });

    await page.goto('/');
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();

    // ⌘K / Ctrl-K from anywhere.
    await page.keyboard.press('ControlOrMeta+k');
    const field = page.getByRole('combobox', { name: 'Rezepte durchsuchen' });
    await expect(field).toBeFocused();

    await field.fill('vegetarisch unter 30 Minuten');
    const chips = page.getByRole('list', { name: 'Verstanden als' });
    await expect(chips.getByRole('button', { name: '„Vegetarisch“ entfernen' })).toBeVisible();
    await expect(page.getByText('Zutat: Sommergemüse')).toBeVisible();

    await chips.getByRole('button', { name: '„Vegetarisch“ entfernen' }).click();
    await expect.poll(() => asked.at(-1)).toBe('unter 30 Minuten');
    await expect(field).toHaveValue('unter 30 Minuten');
    await expect(field).toBeFocused();

    // The arrow keys walk the results with the focus still in the field.
    await field.press('ArrowDown');
    await expect(field).toHaveAttribute('aria-activedescendant', /.+/);
    await field.press('Enter');
    await expect(page).toHaveURL(new RegExp(`/recipes/${recipeId}$`));
    await expect(page.getByRole('dialog')).toBeHidden();
  });

  test('Tab takes the top completion, and Backspace in an empty field takes the last chip back', async ({
    page
  }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop', 'One keyboard walk is enough.');
    await responsiveData(page);
    const tagsAsked: string[][] = [];

    await page.route('**/api/v1/households/*/completions**', (route) =>
      route.fulfill({
        json: { items: [{ kind: 'tag', label: 'Sommer', slug: 'sommer', recipeCount: 3 }] }
      })
    );
    await page.route('**/api/v1/recipes?**', async (route) => {
      const url = new URL(route.request().url());

      if (url.searchParams.get('query') === null && url.searchParams.getAll('tag').length === 0) {
        return route.fallback();
      }

      tagsAsked.push(url.searchParams.getAll('tag'));

      return route.fulfill({ json: { items: [], total: 0 } });
    });

    await page.goto('/');
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();

    await page.keyboard.press('ControlOrMeta+k');
    const field = page.getByRole('combobox', { name: 'Rezepte durchsuchen' });
    await field.fill('somm');
    await expect(page.getByRole('option', { name: /Sommer/ })).toBeVisible();

    // Taken into the search without leaving the field: the half-typed word
    // becomes the tag it was on its way to.
    await field.press('Tab');
    const chip = page.getByRole('button', { name: '„Sommer“ entfernen' });
    await expect(chip).toBeVisible();
    await expect(field).toHaveValue('');
    await expect(field).toBeFocused();
    await expect.poll(() => tagsAsked.at(-1)).toEqual(['sommer']);

    // Nothing left to delete in the field, so the chip goes.
    await field.press('Backspace');
    await expect(chip).toBeHidden();
    await expect(field).toBeFocused();
  });

  test('opens on "/" but never on the cooking screen', async ({ page }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop', 'Keyboard shortcuts.');
    await responsiveData(page);

    await page.goto('/plan');
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
    await page.keyboard.press('/');
    await expect(page.getByRole('combobox', { name: 'Rezepte durchsuchen' })).toBeFocused();
    await page.keyboard.press('Escape');
    await expect(page.getByRole('dialog')).toBeHidden();
    // Searching from the plan must leave the plan exactly as it was.
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();

    await page.goto(`/recipes/${recipeId}/cook`);
    await expect(page.getByRole('button', { name: /nächster schritt/i })).toBeEnabled();
    await expect(page.getByRole('button', { name: 'Rezepte durchsuchen' })).toHaveCount(0);
    await page.keyboard.press('ControlOrMeta+k');
    await expect(page.getByRole('dialog')).toHaveCount(0);
  });

  test('offers the header search button on a phone', async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 812 });
    await responsiveData(page);
    await page.route('**/api/v1/households/*/completions**', (route) =>
      route.fulfill({ json: { items: [] } })
    );

    await page.goto('/shopping');
    await page.getByRole('button', { name: 'Rezepte durchsuchen' }).click();
    const field = page.getByRole('combobox', { name: 'Rezepte durchsuchen' });

    await expect(field).toBeFocused();
    await expect(page.getByRole('heading', { name: 'Schnellzugriff' })).toBeVisible();
    const box = (await field.boundingBox())!;
    expect(box.x).toBeGreaterThanOrEqual(0);
    expect(box.x + box.width).toBeLessThanOrEqual(375);
  });
});
