import { expect, type APIRequestContext, type Browser, type Page } from '@playwright/test';

/**
 * What every flow needs before it can be about anything.
 *
 * Each spec seeds its own data through the API rather than through the screens
 * it is testing: a create test that fails should say the create screen is
 * broken, not that eleven other tests are.
 */

/** The account the suite signs in as. An administrator, so it can open registration. */
export const credentials = {
  email: process.env['CULINA_E2E_EMAIL'],
  password: process.env['CULINA_E2E_PASSWORD']
};

export const needsBackend = !credentials.email || !credentials.password;

/** Where the built app is served. Playwright's baseURL, for request contexts. */
const origin = 'http://localhost:4173';

export const skipReason =
  'Set CULINA_E2E_EMAIL and CULINA_E2E_PASSWORD to an administrator account, with a backend running.';

/**
 * A name no other run, project or worker can be holding.
 *
 * These suites share one instance and one household. A test that asserts on
 * "Butter" is asserting on whatever the browser next to it is doing.
 */
export const unique = (word: string): string =>
  `${word} ${Date.now().toString(36)}${Math.random().toString(36).slice(2, 6)}`;

/**
 * An account of this suite's own, made once and reused after that.
 *
 * Some state belongs to a person and cannot be shared: only one recipe can be
 * being cooked at a time, so two browsers driving one account are two browsers
 * fighting over the same cook session. A flow that needs to own that state asks
 * for an account named after itself.
 *
 * Reused rather than made fresh each run because registration is rate limited
 * per address — as it should be — and a suite that burns five registrations an
 * hour would lock itself out. Remembered within a worker for the same reason:
 * signing in is rate limited per account, and checking whether an account
 * exists costs a sign-in.
 */
const known = new Map<string, { email: string; password: string }>();

export async function ensureAccount(
  browser: Browser,
  name: string
): Promise<{ email: string; password: string }> {
  const remembered = known.get(name);

  if (remembered) {
    return remembered;
  }

  const who = { email: `${name}@culina.test`, password: credentials.password! };
  const context = await browser.newContext();

  try {
    const signedIn = await context.request.post('/api/v1/sessions', {
      headers: { Origin: origin },
      data: who
    });

    if (signedIn.ok()) {
      known.set(name, who);

      return who;
    }

    await openRegistration(browser);

    const created = await context.request.post('/api/v1/users', {
      headers: { Origin: origin },
      data: { email: who.email, displayName: name, password: who.password }
    });

    expect(created.ok(), await created.text()).toBe(true);
    known.set(name, who);

    return who;
  } finally {
    await context.close();
  }
}

/** The instance has to be told to accept new accounts, by somebody who may. */
async function openRegistration(browser: Browser): Promise<void> {
  const context = await browser.newContext();

  try {
    const signedIn = await context.request.post('/api/v1/sessions', {
      headers: { Origin: origin },
      data: credentials
    });

    expect(signedIn.ok(), 'CULINA_E2E_* must be an administrator: ' + (await signedIn.text())).toBe(
      true
    );

    const csrf = (await context.cookies()).find((cookie) => cookie.name === 'culina.csrf')?.value;

    const opened = await context.request.put('/api/v1/settings/registration', {
      headers: { Origin: origin, 'X-Culina-CSRF': csrf ?? '' },
      data: { openRegistration: true, requireInvitation: false, maxUsers: 100 }
    });

    expect(opened.ok(), await opened.text()).toBe(true);
  } finally {
    await context.close();
  }
}

export async function signIn(page: Page, who = credentials): Promise<void> {
  await page.goto('/login');
  await page.getByLabel(/email|e-mail/i).fill(who.email!);
  await page.getByLabel(/password|passwort/i).fill(who.password!);
  await page.getByRole('button', { name: /^(sign in|anmelden)$/i }).click();

  // A fresh account has no household yet, and the app says so rather than
  // pretending. Either landing place is a successful sign-in.
  await expect(page).toHaveURL(/\/(welcome)?$/);
}

/** Signs in, creating the household this account does not have yet. */
export async function signInWithHousehold(page: Page, who = credentials): Promise<void> {
  await signIn(page, who);

  if (new URL(page.url()).pathname === '/welcome') {
    await page.getByLabel(/name your household|heißen/i).fill(unique('Kitchen'));
    await page.getByRole('button', { name: /^(create a household|haushalt anlegen)$/i }).click();
    await expect(page).toHaveURL(/\/$/);
  }
}

/**
 * The headers an unsafe request needs, borrowed from the browser's own session.
 *
 * `page.request` shares the page's cookie jar, which the test runner's
 * top-level `request` fixture does not.
 */
export async function writeHeaders(page: Page): Promise<Record<string, string>> {
  const cookies = await page.context().cookies();

  return {
    'X-Culina-CSRF': cookies.find((cookie) => cookie.name === 'culina.csrf')?.value ?? '',
    Origin: new URL(page.url()).origin
  };
}

export async function householdId(api: APIRequestContext): Promise<string> {
  const me = await api.get('/api/v1/users/me');

  expect(me.ok(), await me.text()).toBe(true);

  return (await me.json()).households[0].householdId;
}

export interface SeedIngredient {
  readonly quantity?: number;
  readonly unit?: string;
  readonly name: string;
}

export interface SeedRecipe {
  readonly title: string;
  readonly yieldAmount?: number;
  readonly ingredients?: readonly SeedIngredient[];
  /** Step text. `{0}` in it is replaced by a reference to ingredient 0. */
  readonly steps?: readonly string[];
}

/**
 * Writes a whole recipe in one call and returns its id.
 *
 * Steps carry references to ingredients rather than repeating their amounts —
 * that is the whole reason scaling a recipe cannot make the list and the steps
 * disagree — so a step written as "Melt {0}" becomes a text segment and an
 * ingredient segment.
 */
export async function seedRecipe(page: Page, recipe: SeedRecipe): Promise<string> {
  const headers = await writeHeaders(page);
  const household = await householdId(page.request);

  const created = await page.request.post('/api/v1/recipes', {
    headers,
    data: { householdId: household, title: recipe.title }
  });

  expect(created.ok(), await created.text()).toBe(true);

  const { recipeId } = await created.json();
  const read = await page.request.get(`/api/v1/recipes/${recipeId}`);

  const ingredients = recipe.ingredients ?? [];
  const saved = await page.request.put(`/api/v1/recipes/${recipeId}`, {
    headers: { ...headers, 'If-Match': read.headers()['etag']! },
    data: {
      title: recipe.title,
      language: 'en',
      yieldAmount: recipe.yieldAmount ?? 2,
      yieldKind: 'servings',
      groups: [{ ingredients }],
      steps: [],
      tags: []
    }
  });

  expect(saved.ok(), await saved.text()).toBe(true);

  if (!recipe.steps?.length) {
    return recipeId;
  }

  // A second write, because a step can only reference an ingredient that
  // already has an id — which it gets from the first one. The stored
  // ingredients go back as they came, ids included: sending them without would
  // read as "these are new and the old ones are gone", and the server rightly
  // refuses to remove an ingredient a step still mentions.
  const withIds = await page.request.get(`/api/v1/recipes/${recipeId}`);
  const stored = await withIds.json();
  const kept = stored.groups[0].ingredients as { ingredientId: string }[];
  const steps = recipe.steps.map((text) => ({
    segments: toSegments(
      text,
      kept.map((one) => one.ingredientId)
    )
  }));

  const withSteps = await page.request.put(`/api/v1/recipes/${recipeId}`, {
    headers: { ...headers, 'If-Match': withIds.headers()['etag']! },
    data: {
      title: stored.title,
      language: stored.language,
      yieldAmount: stored.yieldAmount,
      yieldKind: stored.yieldKind,
      groups: [{ ingredients: kept }],
      steps,
      tags: []
    }
  });

  expect(withSteps.ok(), await withSteps.text()).toBe(true);

  return recipeId;
}

/** Turns "Melt {0} in a pan" into text and ingredient segments. */
function toSegments(text: string, ingredientIds: readonly string[]) {
  return text
    .split(/(\{\d+\})/)
    .filter((part) => part.length > 0)
    .map((part) => {
      const reference = /^\{(\d+)\}$/.exec(part);

      return reference
        ? { type: 'ingredient', recipeIngredientId: ingredientIds[Number(reference[1])] }
        : { type: 'text', value: part };
    });
}
