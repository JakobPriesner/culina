import { expect, type APIRequestContext, type Browser, type Page } from '@playwright/test';

/** Shared e2e helpers: specs seed their data through the API, not the screens under test. */

/** The account the suite signs in as; an administrator, so it can open registration. */
export const credentials = {
  email: process.env['CULINA_E2E_EMAIL'],
  password: process.env['CULINA_E2E_PASSWORD']
};

export const needsBackend = !credentials.email || !credentials.password;

const origin = 'http://localhost:4173';

export const skipReason =
  'Set CULINA_E2E_EMAIL and CULINA_E2E_PASSWORD to an administrator account, with a backend running.';

/** A name unique per run, project and worker; the suites share one instance and household. */
export const unique = (word: string): string =>
  `${word} ${Date.now().toString(36)}${Math.random().toString(36).slice(2, 6)}`;

/**
 * The name of a library card's link, in either language; the title can appear on the page twice.
 */
export const opens = (title: string): RegExp => new RegExp(`^(Open ${title}|${title} öffnen)$`);

/**
 * An account of this suite's own, made once and reused: registration and sign-in are rate limited,
 * and
 * only one recipe can be cooked per account at a time.
 */
const known = new Map<string, { email: string; password: string }>();

/**
 * The account this spec file owns, on this viewport; one per flow so sign-in rate limits and
 * households stay separate.
 */
export function accountFor(browser: Browser, test: { file: string; project: { name: string } }) {
  // Named for the file, not the test: `title` inside a `beforeAll` is the hook's, which would make
  // one account per test.
  const spec = test.file
    .split(/[/\\]/)
    .pop()!
    .replace(/\.spec\.ts$/, '');

  return ensureAccount(browser, `${spec}-${test.project.name}`);
}

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

    const created = await context.request.post('/api/v1/users', {
      headers: { Origin: origin },
      data: { email: who.email, displayName: name, password: who.password }
    });

    // `known` is per worker, so two workers can both create the account; the loser gets a 409 for
    // credentials that now exist.
    const madeElsewhere = created.status() === 409;

    expect(created.ok() || madeElsewhere, await created.text()).toBe(true);
    known.set(name, who);

    return who;
  } finally {
    await context.close();
  }
}

export async function signIn(page: Page, who = credentials): Promise<void> {
  await page.goto('/login');
  await page.getByLabel(/email|e-mail/i).fill(who.email!);
  await page.getByRole('textbox', { name: /password|passwort/i }).fill(who.password!);
  await page.getByRole('button', { name: /(^|\s)(sign (me )?in|anmelden)$/i }).click();

  // A fresh account has no household yet; either landing place is a successful sign-in.
  await expect(page).toHaveURL(/\/(welcome)?$/);
}

/** Signs in, creating the household this account does not have yet. */
export async function signInWithHousehold(page: Page, who = credentials): Promise<void> {
  await signIn(page, who);

  if (new URL(page.url()).pathname === '/welcome') {
    await page.getByLabel(/name your household|heißen/i).fill(unique('Kitchen'));
    await page.getByRole('button', { name: /^(create a household|haushalt erstellen)$/i }).click();
    await expect(page).toHaveURL(/\/$/);
  }
}

/**
 * The headers an unsafe request needs, borrowed from the page's session (`page.request` shares its
 * cookie jar).
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

/** Makes a cookbook and puts a recipe on it, returning its id. */
export async function seedCookbook(page: Page, name: string, recipeId?: string): Promise<string> {
  const headers = await writeHeaders(page);
  const household = await householdId(page.request);

  const created = await page.request.post('/api/v1/cookbooks', {
    headers,
    data: { householdId: household, name }
  });

  expect(created.ok(), await created.text()).toBe(true);

  const { cookbookId } = await created.json();

  if (recipeId) {
    const added = await page.request.put(`/api/v1/cookbooks/${cookbookId}/recipes/${recipeId}`, {
      headers
    });

    expect(added.ok(), await added.text()).toBe(true);
  }

  return cookbookId;
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
  readonly steps?: readonly string[];
  readonly tags?: readonly string[];
}

/**
 * Writes a whole recipe in one call and returns its id; steps reference ingredients, so "Melt {0}"
 * becomes text plus an ingredient segment.
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
      tags: recipe.tags ?? []
    }
  });

  expect(saved.ok(), await saved.text()).toBe(true);

  if (!recipe.steps?.length) {
    return recipeId;
  }

  // A second write: a step can only reference an ingredient that has an id. Stored ingredients are
  // sent back with ids, or the server refuses to remove one a step mentions.
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
      tags: recipe.tags ?? []
    }
  });

  expect(withSteps.ok(), await withSteps.text()).toBe(true);

  return recipeId;
}

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
