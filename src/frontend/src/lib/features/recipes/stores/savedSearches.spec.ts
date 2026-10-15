import { beforeEach, describe, expect, it, vi } from 'vitest';

import { savedSearches, worthSaving } from './savedSearches.svelte';

/* The store behind the toolbar's saved-search chips: the round trip must return exactly what was sent, or a saved search cannot be trusted to reopen. */
const household = 'h1';

const wire = (id: string, name: string, criteria: Record<string, unknown>) => ({
  searchId: id,
  householdId: household,
  name,
  criteria,
  createdBy: 'u1',
  createdAt: '2026-09-18T00:00:00Z',
  updatedAt: '2026-09-18T00:00:00Z'
});

const json = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' }
  });

/** Answers every call, and keeps the body of the first one. */
function recording(reply: Response) {
  const sent: string[] = [];

  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: Request) => {
      sent.push(await input.text());

      return reply.clone();
    })
  );

  return () => JSON.parse(sent[0] ?? '{}');
}

beforeEach(() => {
  savedSearches.reset();
  vi.unstubAllGlobals();
});

describe('saved searches', () => {
  it('reads what the server stored back into the app’s own words', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(() =>
        Promise.resolve(
          json({
            items: [
              wire('s1', 'Schnell', {
                query: 'auflauf',
                tags: ['vegetarisch'],
                maxMinutes: 30,
                sort: 'totalMinutes'
              })
            ]
          })
        )
      )
    );

    await savedSearches.load(household);

    expect(savedSearches.items).toEqual([
      {
        id: 's1',
        name: 'Schnell',
        query: 'auflauf',
        tags: ['vegetarisch'],
        maxMinutes: 30,
        // A saved search that set no calorie ceiling reads back as none.
        maxKcal: null,
        // The wire says `totalMinutes`; above this line it is `quickest`.
        sort: 'quickest'
      }
    ]);
  });

  it('fills in what a saved search did not say', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(() => Promise.resolve(json({ items: [wire('s1', 'Nur Zeit', { maxMinutes: 15 })] })))
    );

    await savedSearches.load(household);

    expect(savedSearches.items[0]).toMatchObject({ query: '', tags: [], sort: null });
  });

  it('asks only once per household', async () => {
    // Called from an `$effect`: a store that asked again on every read would flood and meet the rate limiter.
    const fetched = vi.fn(() => Promise.resolve(json({ items: [] })));

    vi.stubGlobal('fetch', fetched);

    await savedSearches.load(household);
    await savedSearches.load(household);

    expect(fetched).toHaveBeenCalledTimes(1);
  });

  it('asks again after a failure, rather than staying empty for the session', async () => {
    const fetched = vi.fn(() => Promise.resolve(json({ code: 'oops' }, 500)));

    vi.stubGlobal('fetch', fetched);

    await savedSearches.load(household);
    await savedSearches.load(household);

    expect(fetched).toHaveBeenCalledTimes(2);
  });

  it('sends nothing for the parts that were left empty', async () => {
    const body = recording(json(wire('s2', 'Vegetarisch', { tags: ['vegetarisch'] })));

    await savedSearches.save(household, 'Vegetarisch', {
      query: '   ',
      tags: ['vegetarisch'],
      maxMinutes: null,
      sort: null
    });

    const sent = body();

    // An empty search box is "nothing was asked", not a filter matching everything.
    expect(sent.criteria.query).toBeUndefined();
    expect(sent.criteria.maxMinutes).toBeUndefined();
    expect(sent.criteria.sort).toBeUndefined();
  });

  it('leaves a shelf’s own order behind', async () => {
    const body = recording(json(wire('s3', 'Regal', { tags: ['x'] })));

    await savedSearches.save(household, 'Regal', {
      query: '',
      tags: ['x'],
      maxMinutes: null,
      sort: 'shelf'
    });

    const sent = body();

    // `cookbookOrder` needs a cookbook, and a saved search is applied from the library, which has none.
    expect(sent.criteria.sort).toBeUndefined();
  });

  it('puts a forgotten search back when the server refuses', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(() => Promise.resolve(json({ items: [wire('s1', 'Schnell', { maxMinutes: 30 })] })))
    );
    await savedSearches.load(household);

    vi.stubGlobal(
      'fetch',
      vi.fn(() => Promise.resolve(json({ code: 'nope' }, 500)))
    );

    const failure = await savedSearches.forget('s1');

    expect(failure).not.toBeNull();
    expect(savedSearches.items).toHaveLength(1);
  });
});

describe('whether there is anything to save', () => {
  it('refuses a search that asks for nothing', () => {
    expect(worthSaving({ query: '  ', tags: [], maxMinutes: null, sort: null })).toBe(false);
  });

  it('accepts any one of the four on its own', () => {
    expect(worthSaving({ query: 'x', tags: [], maxMinutes: null, sort: null })).toBe(true);
    expect(worthSaving({ query: '', tags: ['x'], maxMinutes: null, sort: null })).toBe(true);
    expect(worthSaving({ query: '', tags: [], maxMinutes: 30, sort: null })).toBe(true);
    expect(worthSaving({ query: '', tags: [], maxMinutes: null, sort: 'title' })).toBe(true);
  });
});

describe('answers that do not belong any more', () => {
  const list = (...ids: string[]) => ({ items: ids.map((id) => wire(id, id, {})) });

  it('drops a read that lands after the store was reset, so a signed-out list is not shown', async () => {
    const held = Promise.withResolvers<Response>();

    vi.stubGlobal(
      'fetch',
      vi.fn(() => held.promise)
    );

    const loading = savedSearches.load('h1');

    savedSearches.reset();
    held.resolve(json(list('s1')));
    await loading;

    expect(savedSearches.items).toEqual([]);
  });

  it('keeps the household asked for last when the first answers last', async () => {
    const answers: Record<string, (response: Response) => void> = {};

    vi.stubGlobal(
      'fetch',
      vi.fn(
        (input: Request) =>
          new Promise<Response>((resolve) => {
            answers[new URL(input.url).searchParams.get('householdId')!] = resolve;
          })
      )
    );

    const first = savedSearches.load('hA');
    const second = savedSearches.load('hB');

    await vi.waitFor(() => expect(Object.keys(answers)).toHaveLength(2));
    answers.hB!(json(list('b')));
    await second;
    answers.hA!(json(list('a')));
    await first;

    expect(savedSearches.items.map((one) => one.id)).toEqual(['b']);
  });

  it('keeps a search saved meanwhile when a revision is refused, and changes nothing it never changed', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(() => Promise.resolve(json(list('s1'))))
    );
    await savedSearches.load(household);

    const held = Promise.withResolvers<Response>();
    const refuse = () => held.resolve(json({ status: 500 }, 500));

    vi.stubGlobal(
      'fetch',
      vi.fn((input: Request) =>
        input.method === 'PATCH' ? held.promise : Promise.resolve(json(wire('s2', 's2', {})))
      )
    );

    const revising = savedSearches.revise('s1', 'new', {
      query: '',
      tags: [],
      maxMinutes: null,
      sort: null
    });

    await savedSearches.save(household, 's2', {
      query: 'x',
      tags: [],
      maxMinutes: null,
      sort: null
    });
    refuse();
    await revising;

    expect(savedSearches.items.map((one) => one.id)).toEqual(['s1', 's2']);
  });

  it('puts back only the forgotten search when the server refuses', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(() => Promise.resolve(json(list('s1', 's2'))))
    );
    await savedSearches.load(household);

    const held = Promise.withResolvers<Response>();
    const refuse = () => held.resolve(json({ status: 500 }, 500));

    vi.stubGlobal(
      'fetch',
      vi.fn((input: Request) =>
        input.method === 'DELETE' ? held.promise : Promise.resolve(json(wire('s3', 's3', {})))
      )
    );

    const forgetting = savedSearches.forget('s1');

    await savedSearches.save(household, 's3', {
      query: 'x',
      tags: [],
      maxMinutes: null,
      sort: null
    });
    refuse();
    await forgetting;

    expect(savedSearches.items.map((one) => one.id)).toEqual(['s1', 's2', 's3']);
  });

  it('does not bring a forgotten search back after sign-out', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(() => Promise.resolve(json(list('s1'))))
    );
    await savedSearches.load(household);

    const held = Promise.withResolvers<Response>();
    const refuse = () => held.resolve(json({ status: 500 }, 500));

    vi.stubGlobal(
      'fetch',
      vi.fn(() => held.promise)
    );

    const forgetting = savedSearches.forget('s1');

    savedSearches.reset();
    refuse();
    await forgetting;

    expect(savedSearches.items).toEqual([]);
  });
});
