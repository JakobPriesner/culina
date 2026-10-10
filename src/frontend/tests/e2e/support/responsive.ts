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
    // Needed but never named: the longest ingredient name on a step's line is what narrow layouts must survive.
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
  coverPictures: [{ recipeId, imageId: 'image-1' }],
  createdBy: userId,
  createdAt: stamp,
  updatedAt: stamp,
  version: 1
};

type WireIngredient = {
  ingredientId?: string | null;
  quantity?: number | null;
  unit?: string | null;
  name: string;
};

/**
 * What a kitchen table would make of each line: a gram line counts by mass, spoons by density, a count or an
 * amount in nothing known cannot be weighed, a line without an amount has none, and a name the table does not
 * know is unknown. Per-portion numbers are unrounded, as the server sends them.
 */
function nutrition(
  ingredients: WireIngredient[],
  corrections: ReadonlyMap<string, string | null> = new Map()
): components['schemas']['RecipesGetNutritionResponse'] {
  const kcal = [240, 200, 80.9];
  let counted = 0;
  const lines = ingredients.map((one) => {
    const ingredientId = one.ingredientId as string;

    // What the household said the name is: a food, or null for not counting it.
    if (corrections.has(one.name)) {
      const code = corrections.get(one.name);

      if (code === null) {
        return {
          ingredientId,
          status: 'excluded' as const,
          corrected: true,
          canRaiseEnergy: false
        };
      }

      counted += 1;
      return {
        ingredientId,
        status: 'counted' as const,
        food: {
          code: code!,
          nameDe: 'Rapsöl',
          nameEn: 'Rapeseed oil',
          labelDe: 'Rapsöl',
          labelEn: 'rapeseed oil'
        },
        grams: 27,
        via: 'density' as const,
        corrected: true,
        canRaiseEnergy: false,
        energyKcal: 120.5
      };
    }
    const food = (nameDe: string, nameEn: string) => ({
      code: 'X',
      nameDe,
      nameEn,
      labelDe: nameDe,
      labelEn: nameEn
    });
    const long = 'handwerklich gebacken, mit Sonnenblumenkernen und Leinsamen';

    if (one.name.includes('Vorrat'))
      return {
        ingredientId,
        status: 'unknownFood' as const,
        corrected: false,
        canRaiseEnergy: true
      };
    if (one.quantity == null || one.unit == null)
      return { ingredientId, status: 'noAmount' as const, corrected: false, canRaiseEnergy: true };
    if (one.unit === 'g') {
      const energyKcal = kcal[counted] ?? 10;
      counted += 1;
      return {
        ingredientId,
        status: 'counted' as const,
        food: food(`Vollkornbrot, ${long}`, `Wholegrain bread, ${long}`),
        grams: one.quantity,
        via: 'mass' as const,
        corrected: false,
        canRaiseEnergy: false,
        energyKcal
      };
    }
    if (one.unit === 'tbsp') {
      counted += 1;
      return {
        ingredientId,
        status: 'counted' as const,
        food: food('Olivenöl', 'Olive oil'),
        grams: one.quantity * 13.5,
        via: 'density' as const,
        corrected: false,
        canRaiseEnergy: false,
        energyKcal: 80.9
      };
    }
    return {
      ingredientId,
      status: 'amountNotInGrams' as const,
      corrected: false,
      canRaiseEnergy: true
    };
  });
  const complete = counted === ingredients.length;
  const value = (amount: number) => ({ value: amount, atLeast: !complete });

  return {
    per: 'serving',
    yield: 2,
    complete,
    counted,
    lines: ingredients.length,
    values: {
      energyKj: value(2179.5),
      // A correction moves the total, as the server's answer would.
      energyKcal: value(corrections.size > 0 ? 560.2 : 520.9),
      fat: value(31.62),
      saturatedFat: value(19.04),
      carbohydrate: value(7.46),
      sugars: value(3.05),
      protein: value(9.2),
      salt: value(0.456)
    },
    ingredients: lines,
    source: {
      name: 'Bundeslebensmittelschlüssel',
      version: '4.0',
      publisher: 'Max Rubner-Institut',
      licence: 'CC BY 4.0'
    }
  };
}

/** Layout fixtures exercise the real routes without accounts or writes to a backend. */
export async function responsiveData(
  page: Page,
  locale: 'de' | 'en' = 'de',
  {
    extraIngredients = 0,
    activeCooking = true,
    longSteps = false,
    suggestions = 0,
    draw = false,
    // A kitchen's worth of lines for the nutrition panel: spoons of oil, a count, and one with no amount.
    nutritionLines = false,
    // The correction a household makes is refused, to see the rows go back.
    refuseCorrections = false
  } = {}
) {
  const detail = {
    ...recipe,
    steps: longSteps
      ? recipe.steps.map((step) => ({
          ...step,
          segments: [
            {
              type: 'text' as const,
              value: Array.from(
                { length: 8 },
                (_, i) => `Abschnitt ${i + 1}. ${step.segments[0]!.value}`
              ).join('\n\n')
            }
          ]
        }))
      : recipe.steps,
    groups: [
      {
        ingredients: [
          ...recipe.groups[0]!.ingredients,
          ...(nutritionLines
            ? [
                { ingredientId: 'oil', name: 'Olivenöl', quantity: 2, unit: 'tbsp' },
                { ingredientId: 'onion', name: 'Zwiebel', quantity: 1, unit: 'piece' },
                { ingredientId: 'salt', name: 'Salz' }
              ]
            : []),
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
  const corrections = new Map<string, string | null>();
  await page.route('**/api/v1/**', async (route) => {
    const url = new URL(route.request().url());
    const path = url.pathname.replace('/api/v1', '');
    const method = route.request().method();
    const said = /^\/households\/[^/]+\/ingredients\/(.+)$/.exec(path);
    if (said && (method === 'PUT' || method === 'DELETE')) {
      if (refuseCorrections)
        return route.fulfill({
          status: 400,
          contentType: 'application/problem+json',
          json: { type: 'x', title: 'x', status: 400, code: 'nutrition.unknown_food' }
        });
      const name = decodeURIComponent(said[1]!);
      if (method === 'DELETE') corrections.delete(name);
      else corrections.set(name, route.request().postDataJSON().food);
      return route.fulfill({ status: 204 });
    }
    // Whatever is typed, the same few foods: the fixture is not a search engine.
    if (path === '/foods') {
      const items = [
        { code: 'Q111111', nameDe: 'Rapsöl', nameEn: 'Rapeseed oil', energyKcal: 828 },
        { code: 'Q222222', nameDe: 'Sonnenblumenöl', nameEn: 'Sunflower oil', energyKcal: 828 },
        {
          code: 'Q333333',
          nameDe: 'Olivenöl, nativ',
          nameEn: 'Olive oil, virgin',
          energyKcal: 824
        },
        { code: 'Q444444', nameDe: 'Butter', nameEn: 'Butter', energyKcal: 741 }
      ];
      return route.fulfill({ json: { items }, headers: { ETag: '"foods"' } });
    }
    const reply = (json: unknown) => route.fulfill({ json, headers: { ETag: '"v1"' } });
    if (path === '/recipe-intakes/events')
      return route.fulfill({
        contentType: 'text/event-stream',
        body: `data: ${JSON.stringify({ snapshot: true, jobs: [] })}\n\n`
      });
    if (path === '/users/me')
      return reply({
        userId,
        email: 'a.very.long.household.member.address@example.test',
        displayName: 'Alexandra',
        isAdmin: false,
        createdAt: stamp,
        version: 1,
        households: [{ householdId, name: 'Unsere gemeinsame Küche', role: 'owner' }],
        // Present but off: without them the editor crashes instead of hiding the assistant's buttons.
        assistance: { improve: false, draft: false, read: false, draw }
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
    // Asked before sign-in and register, which redirect to setup until an admin exists.
    if (path === '/setup') return reply({ stage: 'complete', startedAt: stamp });
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
    if (path === '/cook-sessions' && route.request().method() === 'POST') {
      activeCooking = true;
      cooking = { ...cooking, ...route.request().postDataJSON() };
      return reply(cooking);
    }
    if (path === '/cook-sessions/current') {
      if (!activeCooking) {
        return route.fulfill({ status: 404, json: { type: 'not-found' } });
      }
      return reply(cooking);
    }
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
    if (path === `/recipes/${recipeId}/nutrition`)
      return reply(nutrition(detail.groups[0]!.ingredients, corrections));
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
    if (path === '/tags' || path.endsWith('/tags')) return reply({ items: [] });
    // The library waits for this answer before listing; unanswered, it stays on its skeleton forever.
    if (path === '/suggestions')
      return reply({
        items: Array.from({ length: suggestions }, (_, i) => ({
          ...recipe,
          recipeId: i === 0 ? recipeId : `recipe-${i}`,
          cookCount: 0
        }))
      });
    if (path.endsWith('/related')) return reply({ items: [] });
    // Empty keeps the editor's tag section the same height every run.
    if (path.endsWith('/tag-suggestions')) return reply({ items: [] });
    // Empty: saved-search chips would change the height of everything below.
    if (path === '/searches') return reply({ items: [] });
    if (path.endsWith('/members'))
      return reply({
        items: [
          {
            userId: 'user-1',
            displayName: 'Alex',
            role: 'owner',
            joinedAt: '2026-01-04T12:00:00Z'
          }
        ]
      });
    // A long-named heir household, so the list and the owner's button are measured at every width.
    if (path.endsWith('/heirs'))
      return reply({
        items: [
          {
            householdId: 'heir-1',
            name: 'Wohngemeinschaft in der Altstadt',
            inheritsFrom: householdId
          }
        ]
      });
    if (path.endsWith('/invitations'))
      return reply({ items: [{ invitationId: 'invite-1', expiresAt: '2026-10-14T12:00:00Z' }] });
    if (path.endsWith('/trash')) return reply({ items: [] });
    if (path === '/households') return reply({ items: [] });
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
        // A shelf may scroll within the page; only real scroll containers qualify, so overflow:hidden cannot hide a regression.
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

/** Leaves guided cooking for Shopping through the app navigation; a phone hides that navigation while cooking, so it goes back to the recipe first (an English page). */
export async function openShoppingFromCooking(page: Page) {
  const nav = page.locator('nav:visible');
  if ((await nav.count()) === 0) {
    await page.getByRole('link', { name: 'Back to recipe', exact: true }).click();
  }
  await nav.getByRole('link', { name: /shopping/i }).click();
}
