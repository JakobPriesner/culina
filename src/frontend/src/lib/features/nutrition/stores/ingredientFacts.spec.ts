import { afterEach, describe, expect, it, vi } from 'vitest';

import { ingredientFacts as store } from './ingredientFacts.svelte';

const onion = { name: 'zwiebel', corrected: false, weights: [{ unit: 'piece', grams: 150 }] };
const butter = {
  name: 'butter',
  corrected: true,
  food: {
    code: 'M1',
    nameDe: 'Süßrahmbutter',
    nameEn: 'Sweet cream butter',
    labelDe: 'Süßrahmbutter',
    labelEn: 'Sweet cream butter'
  },
  weights: []
};

const json = (body: unknown) =>
  new Response(JSON.stringify(body), { headers: { 'Content-Type': 'application/json' } });

interface Call {
  method: string;
  url: string;
  body: string;
}

/** A household whose facts and switch the server holds; writes wait for `release` when asked to. */
function serve(
  options: { items?: unknown[]; useTypicalWeights?: boolean; refuse?: boolean; hold?: boolean } = {}
) {
  const calls: Call[] = [];
  const state = { release: undefined as (() => void) | undefined };

  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: Request) => {
      calls.push({ method: input.method, url: input.url, body: await input.clone().text() });

      if (input.method === 'GET') {
        return input.url.endsWith('/nutrition/ingredients')
          ? json({ items: options.items ?? [onion, butter] })
          : json({ useTypicalWeights: options.useTypicalWeights ?? true });
      }

      if (options.hold) {
        await new Promise<void>((resume) => (state.release = resume));
      }

      return options.refuse
        ? new Response(
            JSON.stringify({ code: 'nutrition.invalid_grams', title: 'x', status: 400 }),
            {
              status: 400,
              headers: { 'Content-Type': 'application/problem+json' }
            }
          )
        : json({ useTypicalWeights: false });
    })
  );

  return { calls, state };
}

afterEach(() => {
  store.reset();
  vi.unstubAllGlobals();
});

describe('what a household said about its ingredients', () => {
  it('reads the facts and the switch, for exactly that household', async () => {
    serve({ useTypicalWeights: false });

    await store.load('h1');

    expect(store.status('h1')).toBe('ready');
    expect(store.facts('h1').map((fact) => fact.name)).toEqual(['zwiebel', 'butter']);
    expect(store.facts('h1')[1]?.food?.labelEn).toBe('Sweet cream butter');
    expect(store.useTypicalWeights('h1')).toBe(false);
    expect(store.facts('h2')).toEqual([]);
    expect(store.status('h2')).toBe('idle');
  });

  it('fails as a whole when either read fails', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async (input: Request) =>
        input.url.endsWith('/nutrition') ? new Response(null, { status: 503 }) : json({ items: [] })
      )
    );

    await store.load('h1');

    expect(store.status('h1')).toBe('failed');
  });

  it('moves the switch at once, and moves it back when the server refuses', async () => {
    const { calls, state } = serve({ hold: true, refuse: true });

    await store.load('h1');

    const done = store.setUseTypicalWeights('h1', false);

    await vi.waitFor(() => expect(state.release).toBeDefined());
    expect(store.useTypicalWeights('h1')).toBe(false);

    state.release?.();

    expect(await done).not.toBeNull();
    expect(store.useTypicalWeights('h1')).toBe(true);
    expect(JSON.parse(calls.find((call) => call.method === 'PUT')!.body)).toEqual({
      useTypicalWeights: false
    });
  });

  it('keeps the switch when the server agrees', async () => {
    serve();
    await store.load('h1');

    expect(await store.setUseTypicalWeights('h1', false)).toBeNull();
    expect(store.useTypicalWeights('h1')).toBe(false);
  });

  it('takes a weight off the list at once, and the name with its last fact', async () => {
    const { calls, state } = serve({ hold: true });

    await store.load('h1');

    const done = store.removeWeight('h1', 'zwiebel', 'piece');

    await vi.waitFor(() => expect(state.release).toBeDefined());
    expect(store.facts('h1').map((fact) => fact.name)).toEqual(['butter']);

    state.release?.();
    await done;

    expect(calls.find((call) => call.method === 'DELETE')?.url).toContain(
      '/households/h1/ingredients/zwiebel/units/piece'
    );
    // The list is read again after the change.
    expect(calls.filter((call) => call.url.endsWith('/nutrition/ingredients'))).toHaveLength(2);
  });

  it('puts the list back when a removal is refused', async () => {
    const { state } = serve({ hold: true, refuse: true });

    await store.load('h1');

    const done = store.removeWeight('h1', 'zwiebel', 'piece');

    await vi.waitFor(() => expect(state.release).toBeDefined());
    state.release?.();

    expect(await done).not.toBeNull();
    expect(store.facts('h1').map((fact) => fact.name)).toEqual(['zwiebel', 'butter']);
  });

  it('shows a new weight at once, by unit', async () => {
    const { calls, state } = serve({ hold: true });

    await store.load('h1');

    const done = store.setWeight('h1', 'zwiebel', 'piece', 90);

    await vi.waitFor(() => expect(state.release).toBeDefined());
    expect(store.facts('h1')[0]?.weights).toEqual([{ unit: 'piece', grams: 90 }]);

    state.release?.();
    await done;

    expect(JSON.parse(calls.find((call) => call.method === 'PUT')!.body)).toEqual({ grams: 90 });
  });

  it('forgets a food choice, and sends null for "do not count"', async () => {
    const { calls } = serve();

    await store.load('h1');
    await store.removeFood('h1', 'butter');
    await store.setFood('h1', 'butter', null);

    expect(calls.find((call) => call.method === 'DELETE')?.url).toContain(
      '/households/h1/ingredients/butter'
    );
    expect(JSON.parse(calls.find((call) => call.method === 'PUT')!.body)).toEqual({ food: null });
  });
});
