import { beforeEach, describe, expect, it, vi } from 'vitest';

import { savedSearches, worthSaving } from './savedSearches.svelte';

/*
 * The store the toolbar's saved-search chips read from.
 *
 * What it has to get right is the round trip: a saved search that came back as
 * anything but what was sent could not be trusted to reopen as the search that
 * was saved, and the whole feature is that promise.
 */
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
        // The wire says `totalMinutes`; everything above this line says
        // `quickest`.
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
    // Called from an `$effect`, so a store that asked again on every read would
    // not hang — it would flood, and then meet the rate limiter.
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

    // An empty search box is "nothing was asked", not a filter matching
    // everything.
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

    // `cookbookOrder` needs a cookbook to be an order of, and a saved search is
    // applied from the library, which has none.
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
    // It would be the library, which is the screen it would be applied from.
    expect(worthSaving({ query: '  ', tags: [], maxMinutes: null, sort: null })).toBe(false);
  });

  it('accepts any one of the four on its own', () => {
    expect(worthSaving({ query: 'x', tags: [], maxMinutes: null, sort: null })).toBe(true);
    expect(worthSaving({ query: '', tags: ['x'], maxMinutes: null, sort: null })).toBe(true);
    expect(worthSaving({ query: '', tags: [], maxMinutes: 30, sort: null })).toBe(true);
    expect(worthSaving({ query: '', tags: [], maxMinutes: null, sort: 'title' })).toBe(true);
  });
});
