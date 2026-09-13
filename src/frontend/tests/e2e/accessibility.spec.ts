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

/**
 * Every flow, by keyboard alone.
 *
 * Not only for people who cannot use a pointer: a keyboard walk is the fastest
 * way to find a control that is a `div`, a focus order that jumps around the
 * page, and a dialog that lets focus escape behind it. If the tab order makes
 * sense, the page structure almost certainly does too.
 */
test.describe('reaching everything with a keyboard', () => {
  test.skip(needsBackend, skipReason);

  let context: BrowserContext;
  let page: Page;
  let recipeId: string;

  test.beforeAll(async ({ browser }, testInfo) => {
    if (needsBackend) {
      return;
    }

    context = await browser.newContext();
    page = await context.newPage();

    await signInWithHousehold(page, await accountFor(browser, testInfo));

    recipeId = await seedRecipe(page, {
      title: unique('Reachable'),
      yieldAmount: 2,
      ingredients: [{ quantity: 200, unit: 'g', name: 'Butter' }],
      steps: ['Melt {0}.', 'Wait.']
    });

    // An earlier describe in this file cooked something else with the same
    // account, and only one recipe can be cooked at a time. Arriving with one
    // already going means the screen starts by abandoning it, which is a race
    // this test has no reason to be running.
    await endAnyCooking(page);
  });

  test.afterAll(async () => {
    await context?.close();
  });

  /** Leaves the account with nothing being cooked. */
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

  /** Everything the tab key reaches, in the order it reaches it. */
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
        // Back to where it started: the whole page has been walked.
        break;
      }

      reached.push(focused);
    }

    return reached;
  }

  test('the first stop is the skip link, on every screen', async () => {
    // Without it, reaching the page content by keyboard means tabbing through
    // the navigation on every single page.
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

    // The controls that matter, in the order somebody would meet them.
    expect(reached.join('\n')).toMatch(/one fewer|eine weniger/i);
    expect(reached.join('\n')).toMatch(/scale to what i have|auf meine menge/i);
    expect(reached.join('\n')).toMatch(/shopping list|einkaufsliste/i);
    expect(reached.join('\n')).toMatch(/start cooking|kochen starten/i);

    // Everything focusable is a real control. A focusable `div` is a control
    // that a screen reader describes as nothing at all.
    const tags = new Set(reached.map((entry) => entry.split(':')[0]));

    expect([...tags].sort()).toEqual(['a', 'button', 'input', 'textarea']);
  });

  test('cooking can be driven without touching the screen', async () => {
    await page.goto(`/recipes/${recipeId}/cook`);
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
    await expect(page.getByText(/step 1 of 2|schritt 1 von 2/i)).toBeVisible();

    await page.getByRole('button', { name: /^(next step|nächster schritt)$/i }).focus();
    await page.keyboard.press('Enter');

    await expect(page.getByText(/step 2 of 2|schritt 2 von 2/i)).toBeVisible();

    // And the control that finishes is where the one that advanced was, so the
    // hand does not have to go looking.
    await expect(page.getByRole('button', { name: /^(i made it|fertig gekocht)$/i })).toBeFocused();
  });
});
