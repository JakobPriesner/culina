import { beforeEach, describe, expect, it, vi } from 'vitest';

import { cookbooks } from './cookbooks.svelte';

/* The store is the only source components read shelves from; what changes the screen before the server agrees (a tick surviving a failed write) is what to test. */
const household = 'h1';

const shelf = (id: string, name: string, recipeCount = 0) => ({
  cookbookId: id,
  name,
  description: null,
  recipeCount,
  coverRecipeIds: [],
  coverPictures: [],
  updatedAt: '2026-09-14T00:00:00Z'
});

const json = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' }
  });

const noContent = (status = 204) => new Response(null, { status });

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

    // A failed refresh must not replace what is being read with an error page.
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

    // Both halves: a count left one too high is wrongness nobody can explain later.
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

    expect([...cookbooks.membersOf('c1')].sort()).toEqual(['r1', 'r2']);
  });

  it('keeps a tick that succeeded when an earlier one fails afterwards', async () => {
    const answers: ((response: Response) => void)[] = [];

    vi.stubGlobal(
      'fetch',
      vi.fn(async (input: Request) =>
        input.method === 'GET'
          ? listOf(shelf('c1', 'Christmas', 2), shelf('c2', 'Quick', 5))
          : new Promise<Response>((resolve) => answers.push(resolve))
      )
    );
    await cookbooks.list(household);

    const first = cookbooks.setOn('r1', { id: 'c1', name: 'Christmas' }, true);
    const second = cookbooks.setOn('r1', { id: 'c2', name: 'Quick' }, true);

    await vi.waitFor(() => expect(answers).toHaveLength(2));
    answers[1]!(noContent());
    await second;
    answers[0]!(json({ code: 'cookbooks.not_found' }, 404));
    await first;

    expect(cookbooks.contains('r1', 'c1')).toBe(false);
    expect(cookbooks.contains('r1', 'c2')).toBe(true);
    expect(cookbooks.membersOf('c2')).toEqual(['r1']);
    expect(cookbooks.items.map((one) => one.recipeCount)).toEqual([2, 6]);
  });
});

describe('deleting a shelf', () => {
  beforeEach(async () => {
    serverAnswers(() => listOf(shelf('c1', 'Christmas'), shelf('c2', 'Weeknights')));
    await cookbooks.list(household);

    serverAnswers(() =>
      json({ ...shelf('c1', 'Christmas'), householdId: household, kind: 'manual', version: 3 })
    );
    await cookbooks.load('c1');
  });

  it('quotes the version it was opened at, so a shelf changed since is not lost', async () => {
    const sent: (string | null)[] = [];
    vi.stubGlobal(
      'fetch',
      vi.fn((input: Request) => {
        sent.push(input.headers.get('If-Match'));

        return Promise.resolve(noContent());
      })
    );

    await cookbooks.remove('c1');

    expect(sent).toEqual(['"v3"']);
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

describe('switching households', () => {
  it('shows the skeleton rather than the other household’s shelves', async () => {
    serverAnswers(() => listOf(shelf('c1', 'Their Christmas', 4)));
    await cookbooks.list('h-theirs');

    serverAnswers(() => listOf(shelf('c2', 'Our weeknights', 2)));
    const reading = cookbooks.list('h-ours');

    expect(cookbooks.items).toEqual([]);
    expect(cookbooks.status).toBe('loading');

    await reading;

    expect(cookbooks.items.map((one) => one.name)).toEqual(['Our weeknights']);
  });

  it('forgets which shelves a recipe is on in the household just left', async () => {
    serverAnswers(() => json({ items: [{ cookbookId: 'c1', name: 'Their Christmas' }] }));
    await cookbooks.loadMemberships('r1', 'h-theirs');

    let answer: (response: Response) => void = () => undefined;
    const pending = new Promise<Response>((resolve) => (answer = resolve));
    vi.stubGlobal(
      'fetch',
      vi.fn(() => pending)
    );

    const reading = cookbooks.loadMemberships('r1', 'h-ours');

    expect(cookbooks.membershipsOf('r1')).toEqual([]);

    answer(json({ items: [] }));
    await reading;
  });
});
