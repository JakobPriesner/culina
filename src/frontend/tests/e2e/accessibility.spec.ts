import AxeBuilder from '@axe-core/playwright';
import { expect, test, type BrowserContext, type Page } from '@playwright/test';

import {
  accountFor,
  needsBackend,
  seedCookbook,
  seedRecipe,
  signInWithHousehold,
  skipReason,
  unique
} from './support/culina';

/** Every route in both modes, checked by axe: a floor, not a standard. */
async function violations(page: Page) {
  const result = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
    .analyze();

  return result.violations.map(
    (violation) =>
      `${violation.id} (${violation.impact}): ${violation.help}\n  ${violation.nodes
        .map((node) => node.target.join(' '))
        .join('\n  ')}`
  );
}

/*
 * Motion off: axe sampling mid-transition (the cooking advance button crossing disabled -> accent over 120ms) reported a false contrast failure.
 * app.css collapses transitions under reduced motion.
 */
test.use({ reducedMotion: 'reduce' });

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
  let cookbookId: string;

  test.beforeAll(async ({ browser }, testInfo) => {
    if (needsBackend) {
      return;
    }

    // Explicit context: axe refuses a page made straight from the browser.
    context = await browser.newContext({ reducedMotion: 'reduce' });
    page = await context.newPage();

    await signInWithHousehold(page, await accountFor(browser, testInfo));

    recipeId = await seedRecipe(page, {
      title: unique('Accessible'),
      yieldAmount: 2,
      ingredients: [{ quantity: 200, unit: 'g', name: 'Butter' }],
      steps: ['Melt {0} slowly.', 'Let it cool.']
    });

    cookbookId = await seedCookbook(page, unique('Accessible shelf'), recipeId);

    await page.goto('/shopping');

    await page.getByRole('textbox', { name: /^(amount|menge)$/i }).fill('500');
    await page.getByRole('combobox', { name: /^(unit|einheit)$/i }).fill('g');

    const what = page.getByRole('combobox', { name: /what to buy|was du brauchst/i });

    await what.fill('Mehl');
    await what.press('Enter');
    await expect(what).toHaveValue('');
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

  test('the cookbooks have no violations', async () => {
    await page.goto('/cookbooks');
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();

    expect(await violations(page)).toEqual([]);
  });

  test('one cookbook has no violations', async () => {
    await page.goto(`/cookbooks/${cookbookId}`);
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();

    expect(await violations(page)).toEqual([]);
  });

  for (const path of ['/me', '/me/appearance', '/me/household']) {
    test(`settings at ${path} has no violations`, async () => {
      await page.goto(path);
      await expect(page.getByRole('heading', { level: 1 })).toBeVisible();

      expect(await violations(page)).toEqual([]);
    });
  }
});

/** No layout shift under a thumb: image boxes and lists reserve space, so arriving content never moves a tap target. */
test.describe('what moves while a page loads', () => {
  test.skip(needsBackend, skipReason);
  test.skip(({ browserName }) => browserName !== 'chromium', 'Layout instrumentation.');

  let context: BrowserContext;
  let page: Page;

  test.beforeAll(async ({ browser }, testInfo) => {
    if (needsBackend) {
      return;
    }

    context = await browser.newContext({ reducedMotion: 'reduce' });
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

            // Shifts a person just caused don't count.
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

      // Google calls 0.1 "good"; everything reserves space, so more than a rounding error means a forgotten box.
      expect(shift).toBeLessThan(0.02);
    });
  }
});

/** Every flow by keyboard alone: finds div controls, jumping focus order and dialogs that leak focus. */
test.describe('reaching everything with a keyboard', () => {
  test.skip(needsBackend, skipReason);

  let context: BrowserContext;
  let page: Page;
  let recipeId: string;

  test.beforeAll(async ({ browser }, testInfo) => {
    if (needsBackend) {
      return;
    }

    context = await browser.newContext({ reducedMotion: 'reduce' });
    page = await context.newPage();

    await signInWithHousehold(page, await accountFor(browser, testInfo));

    recipeId = await seedRecipe(page, {
      title: unique('Reachable'),
      yieldAmount: 2,
      ingredients: [{ quantity: 200, unit: 'g', name: 'Butter' }],
      steps: ['Melt {0}.', 'Wait.']
    });

    // Only one recipe can be cooked at a time; a leftover session would make the screen abandon it first (a race).
    await endAnyCooking(page);
  });

  test.afterAll(async () => {
    await context?.close();
  });

  async function endAnyCooking(who: Page) {
    const current = await who.request.get('/api/v1/cook-sessions/current');

    if (!current.ok()) {
      return;
    }

    const body: { sessionId?: string } = await current.json();
    const cookies = await who.context().cookies();
    const csrf = cookies.find((cookie) => cookie.name === 'culina.csrf')?.value ?? '';

    if (body.sessionId) {
      await who.request.delete(`/api/v1/cook-sessions/${body.sessionId}?completed=false`, {
        headers: { 'X-Culina-CSRF': csrf, Origin: new URL(who.url()).origin }
      });
    }
  }

  async function tabOrder(limit = 40) {
    const reached: string[] = [];

    for (let index = 0; index < limit; index += 1) {
      await page.keyboard.press('Tab');

      const focused = await page.evaluate(() => {
        const element = document.activeElement;

        if (!element || element === document.body) {
          return null;
        }

        const name =
          element.getAttribute('aria-label') ?? element.textContent?.trim().slice(0, 40) ?? '';

        return `${element.tagName.toLowerCase()}:${name}`;
      });

      if (focused === null) {
        break;
      }

      if (reached.includes(focused) && reached[0] === focused) {
        break;
      }

      reached.push(focused);
    }

    return reached;
  }

  test('the first stop is the skip link, on every screen', async () => {
    // Without it, keyboard users tab through the navigation on every page.
    for (const path of ['/', `/recipes/${recipeId}`, '/shopping', '/me']) {
      await page.goto(path);
      await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
      await page.keyboard.press('Tab');

      const first = await page.evaluate(() => document.activeElement?.textContent?.trim());

      expect(first, path).toMatch(/skip to content|zum inhalt/i);
    }
  });

  test('every control on the recipe is reachable, and nothing is a div', async () => {
    await page.goto(`/recipes/${recipeId}`);
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();

    const reached = await tabOrder();

    expect(reached.join('\n')).toMatch(/one fewer|eine weniger/i);
    expect(reached.join('\n')).toMatch(/scale to what i have|auf meine menge/i);
    expect(reached.join('\n')).toMatch(/shopping list|einkaufsliste/i);
    expect(reached.join('\n')).toMatch(/start cooking|kochen starten/i);

    // A focusable div is a control a screen reader describes as nothing. A summary is the native
    // control of a disclosure (the nutrition panel's), announced as one.
    const tags = new Set(reached.map((entry) => entry.split(':')[0]));

    expect([...tags].sort()).toEqual(['a', 'button', 'input', 'summary', 'textarea']);
  });

  test('cooking can be driven without touching the screen', async () => {
    await page.goto(`/recipes/${recipeId}/cook`);
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
    await expect(page.getByText(/step 1 of 2|schritt 1 von 2/i)).toBeVisible();

    const next = page.getByRole('button', { name: /^(next step|nächster schritt)$/i });
    // Unlike click, focus does not wait for a control to become enabled.
    await expect(next).toBeEnabled();
    await next.focus();
    await expect(next).toBeFocused();
    await page.keyboard.press('Enter');

    await expect(page.getByText(/step 2 of 2|schritt 2 von 2/i)).toBeVisible();

    // Focus follows the cook to the new step so a screen reader reads it.
    await expect(page.locator('[aria-current="step"]')).toBeFocused();
    await expect(page.getByRole('button', { name: /^(i made it|fertig gekocht)$/i })).toBeEnabled();
  });
});
