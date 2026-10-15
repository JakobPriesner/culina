import { fileURLToPath } from 'node:url';
import { expect, type Locator, type Page } from '@playwright/test';
// API fixtures deliberately model the wire response, not a rendered component.
// eslint-disable-next-line no-restricted-imports
import type { components } from '../../../src/lib/api/generated/schema';

const stamp = '2026-09-14T12:00:00Z';
export const recipeId = '00000000-0000-4000-8000-000000000001';
const householdId = '00000000-0000-4000-8000-000000000002';
/** The share token of the recipe a signed-out visitor is sent. */
export const shareToken = 'a-share-token';
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

/** The household's side of the weights: whether typical ones count, and what it said one unit weighs ("zwiebel|piece"). */
type Weighing = { typical: boolean; weights: ReadonlyMap<string, number> };

/** The kinds of milk the fixture offers, per 100 g. */
const milks = [
  { code: 'H1', labelDe: 'Vollmilch 3,5 %', labelEn: 'whole milk 3.5 %', kcal: 65 },
  { code: 'H2', labelDe: 'fettarme Milch 1,5 %', labelEn: 'low-fat milk 1.5 %', kcal: 47 },
  { code: 'H3', labelDe: 'Magermilch', labelEn: 'skimmed milk', kcal: 35 }
];

/** What the server makes of a name: one key for every spelling. */
const fold = (name: string) => name.trim().toLowerCase().replace('ü', 'ue');

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
  corrections: ReadonlyMap<string, string | null> = new Map(),
  yieldAmount = 2,
  weighing?: Weighing
): components['schemas']['RecipesGetNutritionResponse'] {
  const kcal = [240, 200, 80.9];
  const zeroEnergy = ['Salz'];
  let counted = 0;
  // What the milk and the onion add to the figure, which depends on the kind and on the weight.
  let extraKcal = 0;
  let estimated = false;
  const lines = ingredients.map((one) => {
    const ingredientId = one.ingredientId as string;

    // Milk comes in kinds, one tap apart; a litre of it per pot is the implausible case, not this.
    if (weighing && one.name === 'Milch' && one.unit === 'ml') {
      const chosen = corrections.get('Milch') ?? 'H1';
      const kind = milks.find((milk) => milk.code === chosen)!;
      const grams = one.quantity! * 1.03;

      counted += 1;
      extraKcal += (grams * kind.kcal) / 100;

      return {
        ingredientId,
        status: 'counted' as const,
        food: {
          code: kind.code,
          source: 'bls',
          nameDe: kind.labelDe,
          nameEn: kind.labelEn,
          labelDe: kind.labelDe,
          labelEn: kind.labelEn
        },
        grams,
        via: 'density' as const,
        corrected: corrections.has('Milch'),
        canRaiseEnergy: false,
        energyKcal: (grams * kind.kcal) / 100,
        variants: milks.map((milk) => ({
          code: milk.code,
          labelDe: milk.labelDe,
          labelEn: milk.labelEn,
          energyKcal: milk.kcal
        }))
      };
    }

    // A count is the household's weight, else a typical one (an estimate) unless that is off, else not counted.
    if (weighing && one.unit === 'piece' && one.quantity != null) {
      const own = weighing.weights.get(`${fold(one.name)}|piece`);
      const per = own ?? (weighing.typical ? 110 : null);

      if (per === null)
        return {
          ingredientId,
          status: 'amountNotInGrams' as const,
          reason: 'count' as const,
          unitKey: 'piece',
          corrected: false,
          canRaiseEnergy: true
        };

      const grams = per * one.quantity;

      counted += 1;
      extraKcal += grams * 0.4;
      estimated ||= own === undefined;

      return {
        ingredientId,
        status: 'counted' as const,
        food: {
          code: 'G410100',
          source: 'bls',
          nameDe: 'Zwiebel, roh',
          nameEn: 'Onion, raw',
          labelDe: 'Zwiebel',
          labelEn: 'onion'
        },
        grams,
        via: own === undefined ? ('typicalWeight' as const) : ('householdWeight' as const),
        source: own === undefined ? 'USDA FoodData Central' : undefined,
        unitKey: 'piece',
        corrected: false,
        canRaiseEnergy: false,
        energyKcal: grams * 0.4
      };
    }

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
          source: 'bls',
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
    // The table's long names are the citation; what the reader calls it is shorter.
    const food = (nameDe: string, nameEn: string, labelDe = nameDe, labelEn = nameEn) => ({
      code: 'X',
      source: 'bls',
      nameDe,
      nameEn,
      labelDe,
      labelEn
    });
    const long = 'handwerklich gebacken, mit Sonnenblumenkernen und Leinsamen';

    if (one.name.includes('Vorrat'))
      return {
        ingredientId,
        status: 'unknownFood' as const,
        corrected: false,
        canRaiseEnergy: true
      };
    // As the server answers: an uncounted line can raise the energy unless its food has none (salt: 0 kcal).
    if (one.quantity == null || one.unit == null)
      return {
        ingredientId,
        status: 'noAmount' as const,
        corrected: false,
        canRaiseEnergy: !zeroEnergy.includes(one.name)
      };
    if (one.unit === 'g') {
      const energyKcal = kcal[counted] ?? 10;
      counted += 1;
      return {
        ingredientId,
        status: 'counted' as const,
        food: food(
          `Vollkornbrot, ${long}`,
          `Wholegrain bread, ${long}`,
          'Vollkornbrot',
          'wholegrain bread'
        ),
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
    // A litre of milk for a pot for two is a typo for a decilitre or two; the server does not count it.
    if (one.unit === 'l')
      return {
        ingredientId,
        status: 'implausible' as const,
        grams: one.quantity * 1030,
        via: 'density' as const,
        corrected: false,
        canRaiseEnergy: true
      };
    return {
      ingredientId,
      status: 'amountNotInGrams' as const,
      reason: one.unit === 'piece' ? ('count' as const) : ('spoonOfSolid' as const),
      corrected: false,
      canRaiseEnergy: true
    };
  });
  const complete = counted === ingredients.length;
  const value = (amount: number, rough = false) => ({
    value: amount,
    atLeast: !complete,
    estimated: rough
  });

  return {
    per: 'serving',
    yield: yieldAmount,
    complete,
    counted,
    lines: ingredients.length,
    values: {
      energyKj: value(2179.5),
      // A correction moves the total, as the server's answer would.
      energyKcal: value(
        weighing ? 520.9 + extraKcal : corrections.size > 0 ? 560.2 : 520.9,
        estimated
      ),
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

/**
 * Phones overlay their scrollbars, but desktop Chromium reserves the app's 10px scrollbar in its stable gutter,
 * so a "320px" phone page is 310px wide and the narrow specs measure a layout no phone has. Chromium 156 ignores
 * --enable-features=OverlayScrollbar on Linux (macOS never honoured it), so the page hides its own scrollbar instead.
 * A constructed stylesheet, not a <style> element: the CSP specs refuse an inline style without a nonce.
 */
async function overlayScrollbars(page: Page) {
  await page.addInitScript(() => {
    document.addEventListener('DOMContentLoaded', () => {
      const sheet = new CSSStyleSheet();

      sheet.replaceSync(
        '* { scrollbar-width: none !important; } html { scrollbar-gutter: auto !important; }'
      );
      document.adoptedStyleSheets = [...document.adoptedStyleSheets, sheet];
    });
  });
}

/** Layout fixtures exercise the real routes without accounts or writes to a backend. */
export async function responsiveData(
  page: Page,
  locale: 'de' | 'en' = 'de',
  {
    extraIngredients = 0,
    activeCooking = true,
    longSteps = false,
    // A recipe of this many steps, cooked from its last one, so the step counter has its widest digits.
    stepCount = 0,
    suggestions = 0,
    // A line with a long name and an amount beside it, for the narrowest rows.
    longShoppingLine = false,
    // What a card says about the energy it has: a lower bound, resting partly on typical weights, or both.
    calories = { atLeast: true, estimated: false },
    draw = false,
    // A kitchen's worth of lines for the nutrition panel: spoons of oil, a count, and one with no amount.
    nutritionLines = false,
    // The correction a household makes is refused, to see the rows go back.
    refuseCorrections = false,
    // What the panel has to say besides the usual: an amount that cannot be right, or a recipe for one portion.
    nutritionVariant = 'usual' as 'usual' | 'implausible' | 'whole',
    // A count is counted by a typical weight, milk comes in kinds, and the household can say what a unit weighs.
    estimates = false,
    // A visitor with a share link and no account: the app asks who is signed in and is told nobody.
    signedOut = false,
    // An imported recipe's shape: the lines split into named groups, the second holding the apples.
    twoGroups = false
  } = {}
) {
  await overlayScrollbars(page);

  const detail = {
    ...recipe,
    yieldAmount: nutritionVariant === 'whole' ? 1 : recipe.yieldAmount,
    steps: (stepCount > 0
      ? Array.from({ length: stepCount }, (_, i) => ({
          ...recipe.steps[i % recipe.steps.length]!,
          stepId: `step-${i}`
        }))
      : recipe.steps
    ).map((step) =>
      longSteps
        ? {
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
          }
        : step
    ),
    groups: [
      {
        ingredients: [
          ...recipe.groups[0]!.ingredients,
          ...(nutritionLines
            ? [
                { ingredientId: 'oil', name: 'Olivenöl', quantity: 2, unit: 'tbsp' },
                { ingredientId: 'onion', name: 'Zwiebel', quantity: 1, unit: 'piece' },
                { ingredientId: 'salt', name: 'Salz' },
                ...(estimates
                  ? [{ ingredientId: 'milk', name: 'Milch', quantity: 200, unit: 'ml' }]
                  : []),
                ...(nutritionVariant === 'implausible'
                  ? [{ ingredientId: 'milk', name: 'Milch', quantity: 1800, unit: 'l' }]
                  : [])
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
      .map((group) => (twoGroups ? { ...group, name: 'Für den Teig' } : group))
      .concat(
        twoGroups
          ? [
              {
                name: 'Für die Füllung',
                ingredients: [{ ingredientId: 'apples', name: 'Äpfel', quantity: 3, unit: 'piece' }]
              }
            ]
          : []
      )
  };
  await page.addInitScript((locale) => localStorage.setItem('culina.locale', locale), locale);
  let cooking = {
    sessionId: 'session-1',
    recipeId,
    recipeTitle: longName,
    servings: 2,
    currentStepIndex: Math.max(stepCount - 1, 0),
    startedAt: stamp,
    lastActiveAt: stamp,
    version: 1
  };
  const corrections = new Map<string, string | null>();
  const household = { typical: true, weights: new Map<string, number>() };
  await page.route('**/api/v1/**', async (route) => {
    const url = new URL(route.request().url());
    const path = url.pathname.replace('/api/v1', '');
    const method = route.request().method();
    const weighed = /^\/households\/[^/]+\/ingredients\/([^/]+)\/units\/([^/]+)$/.exec(path);
    if (weighed && (method === 'PUT' || method === 'DELETE')) {
      const key = `${fold(decodeURIComponent(weighed[1]!))}|${decodeURIComponent(weighed[2]!)}`;
      if (method === 'DELETE') household.weights.delete(key);
      else household.weights.set(key, route.request().postDataJSON().grams);
      return route.fulfill({ status: 204 });
    }
    if (/^\/households\/[^/]+\/nutrition$/.test(path)) {
      if (method === 'PUT') household.typical = route.request().postDataJSON().useTypicalWeights;
      return route.fulfill({ json: { useTypicalWeights: household.typical } });
    }
    // What the household has said, by folded name: the foods it chose and the weights it set.
    if (/^\/households\/[^/]+\/nutrition\/ingredients$/.test(path)) {
      const names = new Set([
        ...[...corrections.keys()].map(fold),
        ...[...household.weights.keys()].map((key) => key.split('|')[0]!)
      ]);
      return route.fulfill({
        json: {
          items: [...names].sort().map((name) => {
            const code = [...corrections].find(([written]) => fold(written) === name)?.[1];
            const kind = milks.find((milk) => milk.code === code);
            return {
              name,
              corrected:
                code !== undefined || [...corrections.keys()].some((k) => fold(k) === name),
              food: kind && {
                code: kind.code,
                source: 'bls',
                nameDe: kind.labelDe,
                nameEn: kind.labelEn,
                labelDe: kind.labelDe,
                labelEn: kind.labelEn
              },
              weights: [...household.weights]
                .filter(([key]) => key.startsWith(`${name}|`))
                .map(([key, grams]) => ({ unit: key.split('|')[1]!, grams }))
            };
          })
        }
      });
    }
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
    if (path === '/users/me' && signedOut)
      return route.fulfill({
        status: 401,
        contentType: 'application/problem+json',
        json: { type: 'x', title: 'x', status: 401, code: 'auth.unauthenticated' }
      });
    if (path === `/shared-recipes/${shareToken}`)
      return route.fulfill({
        json: {
          title: detail.title,
          description: detail.description,
          language: detail.language,
          yieldAmount: detail.yieldAmount,
          yieldKind: detail.yieldKind,
          prepMinutes: detail.prepMinutes,
          cookMinutes: detail.cookMinutes,
          totalMinutes: detail.totalMinutes,
          hasImage: false,
          groups: detail.groups,
          steps: detail.steps,
          tags: detail.tags
        }
      });
    // The visitor is in no household, so no correction is in it.
    if (path === `/shared-recipes/${shareToken}/nutrition`)
      return route.fulfill({
        json: nutrition(detail.groups[0]!.ingredients, new Map(), detail.yieldAmount)
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
        return route.fulfill({ status: 204 });
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
    if (
      path.endsWith('/ingredient-names') ||
      path.endsWith('/ingredient-merges') ||
      path.endsWith('/ingredient-merge-suggestions') ||
      // No words or foods of the household's own: Culina's lists and the food table answer alone.
      path.endsWith('/ingredient-nouns') ||
      path === `/households/${householdId}/foods`
    )
      return reply({ items: [] });
    // Fire and forget from the front page; nothing reads the answer.
    if (path === '/suggestions/impressions') return route.fulfill({ status: 204 });
    if (path === `/recipes/${recipeId}`) return reply(detail);
    if (path === `/recipes/${recipeId}/nutrition`)
      return reply(
        nutrition(
          detail.groups.flatMap((group) => group.ingredients),
          corrections,
          detail.yieldAmount,
          estimates ? household : undefined
        )
      );
    if (path.endsWith('/notes')) return reply({ overall: '', steps: [] });
    if (path.endsWith('/cook-log')) return reply({ count: 0, items: [] });
    if (path === '/recipes')
      return reply({
        items: Array.from({ length: 6 }, (_, i) => ({
          ...recipe,
          calories: nutritionLines ? { value: 520.9, ...calories } : undefined,
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
          calories: nutritionLines ? { value: 520.9, ...calories } : undefined,
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
            isManual: true,
            // From a meal planned for a day long gone, the way a list kept across weeks has them.
            sources: [
              {
                recipeId,
                recipeTitle: longName,
                quantity: 200,
                unit: 'g',
                plannedDate: '2026-09-23',
                plannedSlot: 'dinner'
              }
            ]
          },
          ...(longShoppingLine
            ? [
                {
                  itemId: 'item-2',
                  name: 'Vanillezuckerpäckchen',
                  quantity: 1.67,
                  unit: 'pack',
                  section: 'spices_baking' as const,
                  isChecked: false,
                  isManual: true
                }
              ]
            : [])
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

/** No word of any element's text is split across lines. */
export async function expectWholeWords(elements: Locator) {
  const split = await elements.evaluateAll((all) => {
    const broken: string[] = [];
    for (const el of all) {
      const texts = document.createTreeWalker(el, NodeFilter.SHOW_TEXT);
      for (let node = texts.nextNode(); node; node = texts.nextNode()) {
        for (const word of node.textContent!.matchAll(/\S+/g)) {
          const range = document.createRange();
          range.setStart(node, word.index);
          range.setEnd(node, word.index + word[0].length);
          // A word on one line has rectangles at one height.
          const lines = new Set([...range.getClientRects()].map((rect) => Math.round(rect.top)));
          if (lines.size > 1) broken.push(word[0]);
        }
      }
    }
    return broken;
  });
  expect(split).toEqual([]);
}
