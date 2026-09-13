import AxeBuilder from '@axe-core/playwright';
import { expect, test, type BrowserContext, type Page } from '@playwright/test';

import {
  accountFor,
  needsBackend,
  seedRecipe,
  signInWithHousehold,
  skipReason,
  unique
} from './support/culina';

/**
 * Every route, in both modes, checked by a machine.
 *
 * An automated pass finds the things people stop noticing — a contrast that
 * drifted, a control with no name, a heading level skipped — and finds none of
 * the things that actually matter most, which is why it is a floor and not a
 * standard. What it is good at is not letting the floor move.
 */
async function violations(page: Page) {
  const result = await new AxeBuilder({ page })
    // The published levels, which is what "accessible" means to anybody
    // outside this repository.
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
    .analyze();

  return result.violations.map(
    (violation) =>
      `${violation.id} (${violation.impact}): ${violation.help}\n  ${violation.nodes
        .map((node) => node.target.join(' '))
        .join('\n  ')}`
  );
}

test.describe('what a machine can check @offline', () => {
  for (const [name, path] of [
    ['sign in', '/login'],
    ['register', '/register']
  ] as const) {
    test(`the ${name} page has no violations`, async ({ page }) => {
      await page.goto(path);
      await expect(page.getByRole('heading', { level: 1 })).toBeVisible();

      expect(await violations(page)).toEqual([]);
    });

    test(`the ${name} page has no violations in the dark`, async ({ page }) => {
      await page.addInitScript(() => {
        localStorage.setItem(
          'culina.appearance',
          JSON.stringify({ theme: 'warm-paper', mode: 'dark' })
        );
      });

      await page.goto(path);
      await expect(page.getByRole('heading', { level: 1 })).toBeVisible();

      expect(await violations(page)).toEqual([]);
    });
  }
});

test.describe.configure({ mode: 'serial' });

test.describe('what a machine can check, signed in', () => {
  test.skip(needsBackend, skipReason);

  let context: BrowserContext;
  let page: Page;
  let recipeId: string;

  test.beforeAll(async ({ browser }, testInfo) => {
    if (needsBackend) {
      return;
    }

    // An explicit context, because axe refuses to run in a page that was made
    // straight from the browser.
    context = await browser.newContext();
    page = await context.newPage();

    await signInWithHousehold(page, await accountFor(browser, testInfo));

    recipeId = await seedRecipe(page, {
      title: unique('Accessible'),
      yieldAmount: 2,
      ingredients: [{ quantity: 200, unit: 'g', name: 'Butter' }],
      steps: ['Melt {0} slowly.', 'Let it cool.']
    });

    // Something on the shopping list, so the list is not tested empty.
    await page.goto('/shopping');

    const field = page.getByRole('textbox', { name: /add|hinzufügen/i });

    await field.fill('500 g Mehl');
    await field.press('Enter');
  });

  test.afterAll(async () => {
    await context?.close();
  });

  test('the recipe list has no violations', async () => {
    await page.goto('/');
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();

    expect(await violations(page)).toEqual([]);
  });

  test('a recipe has no violations', async () => {
    await page.goto(`/recipes/${recipeId}`);
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();

    expect(await violations(page)).toEqual([]);
  });

  test('cooking has no violations', async () => {
    await page.goto(`/recipes/${recipeId}/cook`);
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();

    expect(await violations(page)).toEqual([]);
  });

  test('the editor has no violations', async () => {
    await page.goto(`/recipes/${recipeId}/edit`);
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();

    expect(await violations(page)).toEqual([]);
  });

  test('the shopping list has no violations', async () => {
    await page.goto('/shopping');
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();

    expect(await violations(page)).toEqual([]);
  });

  test('the profile has no violations', async () => {
    await page.goto('/me');
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();

    expect(await violations(page)).toEqual([]);
  });
});

/**
 * Nothing moves under a thumb.
 *
 * Every image box reserves its space and every list has a skeleton, precisely
 * so that content arriving does not push what somebody was about to tap. A
 * regression here means one of them was skipped, which is invisible on a fast
 * machine and infuriating on a slow connection.
 */
test.describe('what moves while a page loads', () => {
  test.skip(needsBackend, skipReason);
  test.skip(({ browserName }) => browserName !== 'chromium', 'Layout instrumentation.');

  let context: BrowserContext;
  let page: Page;

  test.beforeAll(async ({ browser }, testInfo) => {
    if (needsBackend) {
      return;
    }

    context = await browser.newContext();
    page = await context.newPage();

    await signInWithHousehold(page, await accountFor(browser, testInfo));
    await seedRecipe(page, {
      title: unique('Settled'),
      yieldAmount: 2,
      ingredients: [{ quantity: 200, unit: 'g', name: 'Butter' }],
      steps: ['Melt {0}.']
    });
  });

  test.afterAll(async () => {
    await context?.close();
  });

  for (const [name, path] of [
    ['recipe list', '/'],
    ['shopping list', '/shopping']
  ] as const) {
    test(`the ${name} settles without shifting`, async () => {
      await page.addInitScript(() => {
        Object.assign(window, { shift: 0 });

        new PerformanceObserver((list) => {
          for (const entry of list.getEntries()) {
            const layout = entry as unknown as { value: number; hadRecentInput: boolean };

            // Anything a person just caused is not a shift they suffered.
            if (!layout.hadRecentInput) {
              (window as unknown as { shift: number }).shift += layout.value;
            }
          }
        }).observe({ type: 'layout-shift', buffered: true });
      });

      await page.goto(path);
      await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
      await page.waitForTimeout(1500);

      const shift = await page.evaluate(() => (window as unknown as { shift: number }).shift);

      // Google calls 0.1 "good". Culina reserves space for everything that
      // arrives late, so anything above a rounding error means a box was
      // forgotten.
      expect(shift).toBeLessThan(0.02);
    });
  }
});
