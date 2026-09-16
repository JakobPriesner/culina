import { fileURLToPath } from 'node:url';
import { expect, type Page } from '@playwright/test';
// API fixtures deliberately model the wire response, not a rendered component.
// eslint-disable-next-line no-restricted-imports
import type { components } from '../../../src/lib/api/generated/schema';

const stamp = '2026-09-14T12:00:00Z';
export const recipeId = '00000000-0000-4000-8000-000000000001';
const householdId = '00000000-0000-4000-8000-000000000002';
export const cookbookId = '00000000-0000-4000-8000-000000000004';
const userId = '00000000-0000-4000-8000-000000000003';
const longName = 'Sonnenblumenkernvollkornbrot mit geröstetem Sommergemüse';

const recipe: components['schemas']['RecipesRecipeDetail'] = {
  recipeId,
  householdId,
  title: longName,
  imageId: 'image-1',
  description: 'Ein gemeinsames Abendessen mit frischen Kräutern und saisonalem Gemüse.',
  language: 'de',
  yieldAmount: 2,
  yieldKind: 'servings',
  prepMinutes: 15,
  cookMinutes: 20,
  totalMinutes: 35,
  tags: ['Familienküche'],
  groups: [
    {
      ingredients: [
        {
          ingredientId: 'ingredient-1',
          quantity: 200,
          unit: 'g',
          name: 'Sonnenblumenkernvollkornbrot',
          note: 'in mundgerechte Stücke geschnitten'
        },
        { ingredientId: 'ingredient-2', quantity: 500, unit: 'g', name: 'Sommergemüse' }
      ]
    }
  ],
  steps: Array.from({ length: 4 }, (_, i) => ({
    stepId: `step-${i}`,
    // Needed but never named — the longest ingredient name in the kitchen, on
    // the line under a step, is what the narrow layouts have to survive.
    uses: i % 2 === 0 ? ['ingredient-1', 'ingredient-2'] : ['ingredient-2'],
    segments: [
      {
        type: 'text' as const,
        value:
          'Das Sommergemüse in gleichmäßige Stücke schneiden. Mit Olivenöl und frischen Kräutern vermischen und langsam goldbraun rösten. Vor dem Servieren abschmecken.'
      }
    ]
  })),
  createdBy: userId,
  createdAt: stamp,
  updatedAt: stamp,
  version: 1
};

const cookbook: components['schemas']['CookbooksCookbookDetail'] = {
  cookbookId,
  householdId,
  name: 'Unsere liebsten Familienrezepte für gemeinsame Abende',
  description: 'Gerichte für kleine und große Runden, über Generationen gesammelt.',
  kind: 'manual',
  recipeCount: 6,
  coverRecipeIds: [recipeId],
  createdBy: userId,
  createdAt: stamp,
  updatedAt: stamp,
  version: 1
};

/** Layout fixtures exercise the real routes without accounts or writes to a backend. */
export async function responsiveData(
  page: Page,
  locale: 'de' | 'en' = 'de',
  { extraIngredients = 0 } = {}
) {
  const detail = {
    ...recipe,
    groups: [
      {
        ingredients: [
          ...recipe.groups[0]!.ingredients,
          ...Array.from({ length: extraIngredients }, (_, i) => ({
            ingredientId: `extra-${i}`,
            name: `Gemüse aus dem Vorrat ${i + 1}`,
            quantity: 100,
            unit: 'g'
          }))
        ]
      }
    ]
  };
  await page.addInitScript((locale) => localStorage.setItem('culina.locale', locale), locale);
  let cooking = {
    sessionId: 'session-1',
    recipeId,
    recipeTitle: longName,
    servings: 2,
    currentStepIndex: 0,
    startedAt: stamp,
    lastActiveAt: stamp,
    version: 1
  };
  await page.route('**/api/v1/**', async (route) => {
    const url = new URL(route.request().url());
    const path = url.pathname.replace('/api/v1', '');
    const reply = (json: unknown) => route.fulfill({ json, headers: { ETag: '"v1"' } });
    if (path === '/users/me')
      return reply({
        userId,
        email: 'a.very.long.household.member.address@example.test',
        displayName: 'Alexandra',
        isAdmin: false,
        createdAt: stamp,
        version: 1,
        households: [{ householdId, name: 'Unsere gemeinsame Küche', role: 'owner' }]
      });
    if (path === '/users/me/settings')
      return reply({
        locale,
        theme: 'warm-paper',
        mode: 'light',
        measurementSystem: 'metric',
        version: 1
      });
    if (path === '/registration/policy')
      return reply({ openRegistration: true, requireInvitation: false, hasAccounts: true });
    if (path === '/cookbooks')
      return reply({
        items: Array.from({ length: 6 }, (_, i) => ({
          ...cookbook,
          cookbookId: i === 0 ? cookbookId : `cookbook-${i}`,
          name: `${cookbook.name} ${i + 1}`
        })),
        nextCursor: null
      });
    if (path === `/cookbooks/${cookbookId}`) return reply(cookbook);
    if (path === `/recipes/${recipeId}/cookbooks`)
      return reply({ items: [{ cookbookId, name: cookbook.name }] });
    if (path === '/cook-sessions/current') return reply(cooking);
    if (path === '/cook-sessions/session-1') {
      cooking = { ...cooking, ...route.request().postDataJSON() };
      return reply(cooking);
    }
    if (path.endsWith('/image'))
      return route.fulfill({
        path: fileURLToPath(new URL('../../../static/images/culina-orzo.webp', import.meta.url)),
        contentType: 'image/webp'
      });
    if (path.endsWith('/timers')) return reply({ items: [] });
    if (path.endsWith('/units')) return reply({ own: [] });
    if (path.endsWith('/ingredients')) return reply({ items: [] });
    if (path === `/recipes/${recipeId}`) return reply(detail);
    if (path.endsWith('/notes')) return reply({ overall: '', steps: [] });
    if (path.endsWith('/cook-log')) return reply({ count: 0, items: [] });
    if (path === '/recipes')
      return reply({
        items: Array.from({ length: 6 }, (_, i) => ({
          ...recipe,
          recipeId: i === 0 ? recipeId : `recipe-${i}`,
          imageId: i < 4 ? 'image-1' : null,
          cookCount: 0
        })),
        total: 6,
        nextCursor: null
      });
    if (path.endsWith('/invitations'))
      return reply({ items: [{ invitationId: 'invite-1', expiresAt: '2026-10-14T12:00:00Z' }] });
    if (path.endsWith('/shopping-list'))
      return reply({
        listId: 'list-1',
        version: 1,
        items: [
          {
            itemId: 'item-1',
            name: 'Sonnenblumenkernvollkornbrot',
            quantity: 200,
            unit: 'g',
            section: 'bakery',
            isChecked: false,
            isManual: true
          }
        ]
      });
    if (path.endsWith('/meal-plan')) {
      const from = url.searchParams.get('from') ?? '2026-09-14';
      return reply({
        from,
        days: Array.from({ length: 7 }, (_, i) => {
          const date = new Date(`${from}T12:00:00Z`);
          date.setUTCDate(date.getUTCDate() + i);
          return {
            date: date.toISOString().slice(0, 10),
            meals: [
              {
                entryId: `meal-${i}`,
                recipeId,
                title: longName,
                totalMinutes: 35,
                recipeServings: 2,
                slot: 'dinner'
              }
            ]
          };
        })
      });
    }
    throw new Error(`Missing responsive fixture: ${route.request().method()} ${path}`);
  });
}

/** Find overflow at its source, including clipped controls inside a fitting page. */
export async function expectReflow(page: Page) {
  const overflow = await page.evaluate(() => {
    const width = document.documentElement.clientWidth;
    return [...document.querySelectorAll<HTMLElement>('main *, dialog[open] *, nav *')]
      .filter((el) => {
        const box = el.getBoundingClientRect();
        const style = getComputedStyle(el);
        if (box.width <= 1 || box.height <= 1 || style.visibility === 'hidden') return false;
        // A shelf may intentionally scroll within the page. Only real scroll
        // containers qualify; overflow:hidden must never conceal a regression.
        let left = box.left;
        let right = box.right;
        const popover = el.closest(':popover-open');
        for (
          let parent = el.parentElement;
          parent && parent !== document.body;
          parent = parent.parentElement
        ) {
          if (popover && !popover.contains(parent)) break;
          const overflow = getComputedStyle(parent).overflowX;
          if (overflow === 'auto' || overflow === 'scroll') {
            const clip = parent.getBoundingClientRect();
            left = Math.max(left, clip.left);
            right = Math.min(right, clip.right);
          }
        }
        return right > left && (left < -1 || right > width + 1);
      })
      .map((el) => `${el.tagName}.${el.className}: ${el.textContent?.trim().slice(0, 50)}`);
  });
  expect(overflow).toEqual([]);
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(
    page.viewportSize()!.width
  );
}
