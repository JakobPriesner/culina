import { beforeEach, describe, expect, it, vi } from 'vitest';

import { cookbooks } from './cookbooks.svelte';

/*
 * The store is the only thing components read shelves from, so what it gets
 * wrong the whole feature gets wrong. The parts worth testing are the ones that
 * change the screen before the server has agreed: a tick that stays ticked
 * after a failed write is a lie about where somebody's recipe is.
 */
const household = 'h1';

const shelf = (id: string, name: string, recipeCount = 0) => ({
  cookbookId: id,
  name,
  description: null,
  recipeCount,
  coverRecipeIds: [],
  updatedAt: '2026-09-14T00:00:00Z'
});

const json = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' }
  });

const noContent = (status = 204) => new Response(null, { status });

/** Answers each request by method and URL. */
function serverAnswers(reply: (method: string, url: string) => Response) {
  vi.stubGlobal(
    'fetch',
    vi.fn((input: Request) => Promise.resolve(reply(input.method, input.url)))
  );
}

const listOf = (...items: ReturnType<typeof shelf>[]) =>
  json({ items, nextCursor: null, total: items.length });

beforeEach(() => {
  cookbooks.reset();
  serverAnswers(() => listOf());
});

describe('a household’s shelves', () => {
  it('reads them once and reports how many are on each', async () => {
    serverAnswers(() => listOf(shelf('c1', 'Christmas', 4)));

    await cookbooks.list(household);

    expect(cookbooks.status).toBe('ready');
    expect(cookbooks.items).toHaveLength(1);
    expect(cookbooks.items[0]).toMatchObject({ id: 'c1', name: 'Christmas', recipeCount: 4 });
  });

  it('keeps the shelves on screen when a refetch fails', async () => {
    serverAnswers(() => listOf(shelf('c1', 'Christmas')));
    await cookbooks.list(household);

    serverAnswers(() => json({ code: 'server.error' }, 500));
    await cookbooks.list(household);

    // Replacing what somebody is reading with an error page loses more than it
    // tells them: the shelves were right a second ago and still are.
    expect(cookbooks.items).toHaveLength(1);
  });
});

describe('putting a recipe on a shelf', () => {
  beforeEach(async () => {
    serverAnswers(() => listOf(shelf('c1', 'Christmas', 2)));
    await cookbooks.list(household);
  });

  it('shows it on the shelf before the server has agreed', async () => {
    serverAnswers((method) => (method === 'PUT' ? noContent() : listOf()));

    const done = await cookbooks.setOn('r1', { id: 'c1', name: 'Christmas' }, true);

    expect(done).toBe(true);
    expect(cookbooks.contains('r1', 'c1')).toBe(true);
    expect(cookbooks.items[0]?.recipeCount).toBe(3);
  });

  it('puts everything back exactly as it was when the write fails', async () => {
    serverAnswers((method) =>
      method === 'PUT' ? json({ code: 'cookbooks.not_found' }, 404) : listOf()
    );

    const done = await cookbooks.setOn('r1', { id: 'c1', name: 'Christmas' }, true);

    // Both halves, not just the tick: a count left one too high is the kind of
    // small wrongness nobody can explain later.
    expect(done).toBe(false);
    expect(cookbooks.contains('r1', 'c1')).toBe(false);
    expect(cookbooks.items[0]?.recipeCount).toBe(2);
  });

  it('takes it off again through the same control', async () => {
    serverAnswers((method) => (method === 'PUT' || method === 'DELETE' ? noContent() : listOf()));

    await cookbooks.setOn('r1', { id: 'c1', name: 'Christmas' }, true);
    await cookbooks.setOn('r1', { id: 'c1', name: 'Christmas' }, false);

    expect(cookbooks.contains('r1', 'c1')).toBe(false);
    expect(cookbooks.items[0]?.recipeCount).toBe(2);
  });
});

describe('what is on a shelf, all of it', () => {
  it('knows every recipe on it, not only the first page of them', async () => {
    serverAnswers((_, url) =>
      url.endsWith('/cookbooks/c1/recipes') ? json({ recipeIds: ['r1', 'r2'] }) : listOf()
    );

    await cookbooks.loadMembers('c1');

    expect(cookbooks.membersOf('c1')).toEqual(['r1', 'r2']);
  });

  it('keeps in step with a tick, and puts it back when the write fails', async () => {
    serverAnswers((method, url) => {
      if (url.endsWith('/cookbooks/c1/recipes')) {
        return json({ recipeIds: ['r1'] });
      }

      return method === 'DELETE' ? json({ code: 'cookbooks.not_found' }, 404) : noContent();
    });

    await cookbooks.loadMembers('c1');
    await cookbooks.setOn('r2', { id: 'c1', name: 'Christmas' }, true);

    expect(cookbooks.membersOf('c1')).toEqual(['r1', 'r2']);

    await cookbooks.setOn('r1', { id: 'c1', name: 'Christmas' }, false);

    expect(cookbooks.membersOf('c1')).toEqual(['r1', 'r2']);
  });
});

describe('deleting a shelf', () => {
  beforeEach(async () => {
    serverAnswers(() => listOf(shelf('c1', 'Christmas'), shelf('c2', 'Weeknights')));
    await cookbooks.list(household);
  });

  it('takes it off the screen at once', async () => {
    serverAnswers((method) => (method === 'DELETE' ? noContent() : listOf()));

    expect(await cookbooks.remove('c1')).toBe(true);
    expect(cookbooks.items.map((item) => item.id)).toEqual(['c2']);
  });

  it('puts it back when the server refuses', async () => {
    serverAnswers((method) =>
      method === 'DELETE' ? json({ code: 'server.error' }, 500) : listOf()
    );

    expect(await cookbooks.remove('c1')).toBe(false);
    expect(cookbooks.items.map((item) => item.id)).toEqual(['c1', 'c2']);
  });
});

describe('signing out', () => {
  it('forgets every shelf, because the next person must not see them', async () => {
    serverAnswers(() => listOf(shelf('c1', 'Christmas')));
    await cookbooks.list(household);

    cookbooks.reset();

    expect(cookbooks.items).toHaveLength(0);
    expect(cookbooks.status).toBe('idle');
    expect(cookbooks.membershipsOf('r1')).toHaveLength(0);
  });
});
