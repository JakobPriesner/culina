import { beforeEach, describe, expect, it, vi } from 'vitest';

import { nouns } from './nouns.svelte';

const json = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' }
  });

const listed = { items: [{ one: 'Brot', many: 'Brote' }] };

beforeEach(() => {
  nouns.reset();
  vi.unstubAllGlobals();
});

/** Lists one noun, then holds every write until `refuse` is called. */
async function withWriteOut() {
  vi.stubGlobal(
    'fetch',
    vi.fn(() => Promise.resolve(json(listed)))
  );
  await nouns.load('h1');

  const held = Promise.withResolvers<Response>();

  vi.stubGlobal(
    'fetch',
    vi.fn(() => held.promise.then((response) => response.clone()))
  );

  return () => held.resolve(json({ status: 500 }, 500));
}

describe('the household’s own nouns', () => {
  it('puts back the noun that was removed, keeping one added meanwhile', async () => {
    const refuse = await withWriteOut();

    const removing = nouns.remove('h1', 'Brot');
    const adding = nouns.set('h1', { one: 'Ei', many: 'Eier' });

    await vi.waitFor(() => expect(nouns.own.map((one) => one.one)).toEqual(['Ei']));
    refuse();
    await removing;

    // The add failed too, so only the first removal is undone once its own write fails; both undo independently.
    await adding;

    expect(nouns.own.map((one) => one.one)).toEqual(['Brot']);
  });

  it('restores the old plural when changing it is refused', async () => {
    const refuse = await withWriteOut();

    const setting = nouns.set('h1', { one: 'Brot', many: 'Brötchen' });

    expect(nouns.own[0]?.many).toBe('Brötchen');
    refuse();
    await setting;

    expect(nouns.own[0]?.many).toBe('Brote');
  });

  it('does not refill the store after sign-out', async () => {
    const refuse = await withWriteOut();

    const removing = nouns.remove('h1', 'Brot');

    nouns.reset();
    refuse();
    await removing;

    expect(nouns.own).toEqual([]);
  });

  it('drops a read that lands after sign-out', async () => {
    const held = Promise.withResolvers<Response>();

    vi.stubGlobal(
      'fetch',
      vi.fn(() => held.promise)
    );

    const loading = nouns.load('h1');

    nouns.reset();
    held.resolve(json(listed));
    await loading;

    expect(nouns.own).toEqual([]);
  });
});
