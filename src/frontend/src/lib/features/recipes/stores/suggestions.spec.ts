import { beforeEach, describe, expect, it, vi } from 'vitest';

import { suggestions } from './suggestions.svelte';

/*
 * The store components read suggestions from, so what it gets wrong the whole
 * feature gets wrong.
 *
 * The first test is the important one and it is not about suggestions at all:
 * this store is called from an `$effect`, and an effect re-runs when anything
 * reactive it read while running changes. A store that guarded itself with
 * `$state` would therefore ask the same question forever — which does not
 * present as a hang, but as a flood of identical requests and then a 429 from
 * the session rate limiter, on a page that looks merely broken.
 */
const household = 'h1';

const suggestion = (id: string, reason: { code: string; subject?: string } | null = null) => ({
  recipeId: id,
  title: `Recipe ${id}`,
  imageId: null,
  totalMinutes: 25,
  yieldAmount: 4,
  yieldKind: 'servings',
  tags: [],
  cookCount: 0,
  lastCookedAt: null,
  updatedAt: '2026-09-18T00:00:00Z',
  reason
});

const answer = (items: ReturnType<typeof suggestion>[]) =>
  new Response(JSON.stringify({ items }), {
    status: 200,
    headers: { 'Content-Type': 'application/json' }
  });

function serverAnswers(reply: (url: string) => Response | Promise<Response>) {
  const fetched = vi.fn((input: Request) => Promise.resolve(reply(input.url)));

  vi.stubGlobal('fetch', fetched);

  return fetched;
}

beforeEach(() => {
  suggestions.reset();
});

describe('asking', () => {
  it('asks the server once per question, however often it is called', async () => {
    const fetched = serverAnswers(() => answer([suggestion('a')]));

    await suggestions.ask(household, { limit: 1 });
    await suggestions.ask(household, { limit: 1 });
    await suggestions.ask(household, { limit: 1 });

    expect(fetched).toHaveBeenCalledTimes(1);
  });

  it('treats a different occasion as a different question', async () => {
    const fetched = serverAnswers(() => answer([suggestion('a')]));

    await suggestions.ask(household, { limit: 1 });
    await suggestions.ask(household, { limit: 1, slot: 'breakfast' });

    expect(fetched).toHaveBeenCalledTimes(2);
  });

  it('does not ask again after a failure, because a retry is a decision', async () => {
    // A guarded call that re-armed itself on failure is the same loop by
    // another name: the end of a failing page stays on screen, asks again, and
    // fails again for the rest of the afternoon.
    const fetched = serverAnswers(() => new Response('{}', { status: 500 }));

    await suggestions.ask(household, { limit: 1 });
    await suggestions.ask(household, { limit: 1 });

    expect(fetched).toHaveBeenCalledTimes(1);
    expect(suggestions.statusOf(household, { limit: 1 })).toBe('failed');
  });

  it('asks again when told to retry', async () => {
    const fetched = serverAnswers(() => answer([suggestion('a')]));

    await suggestions.ask(household, { limit: 1 });
    await suggestions.retry(household, { limit: 1 });

    expect(fetched).toHaveBeenCalledTimes(2);
  });

  it('keeps each occasion’s answer apart', async () => {
    serverAnswers((url) => answer([suggestion(url.includes('breakfast') ? 'morning' : 'evening')]));

    await suggestions.ask(household, { limit: 1 });
    await suggestions.ask(household, { limit: 1, slot: 'breakfast' });

    expect(suggestions.for(household, { limit: 1 })[0]?.id).toBe('evening');
    expect(suggestions.for(household, { limit: 1, slot: 'breakfast' })[0]?.id).toBe('morning');
  });

  it('answers with nothing for a household it has not been asked about', () => {
    expect(suggestions.for(null)).toEqual([]);
    expect(suggestions.for(household)).toEqual([]);
  });
});

describe('dismissing', () => {
  it('removes the card from every answer at once, not just the one on screen', async () => {
    // The same recipe is regularly in two answers. Watching it vanish from one
    // list and stay in another is worse than not having dismissed it.
    serverAnswers(() => answer([suggestion('a'), suggestion('b')]));

    await suggestions.ask(household, { limit: 2 });
    await suggestions.ask(household, { limit: 2, slot: 'dinner' });

    serverAnswers(() => new Response(null, { status: 204 }));
    await suggestions.dismiss('a');

    expect(suggestions.for(household, { limit: 2 }).map((item) => item.id)).toEqual(['b']);
    expect(suggestions.for(household, { limit: 2, slot: 'dinner' }).map((item) => item.id)).toEqual(
      ['b']
    );
  });

  it('puts the card back when the write fails', async () => {
    serverAnswers(() => answer([suggestion('a'), suggestion('b')]));
    await suggestions.ask(household, { limit: 2 });

    serverAnswers(() => new Response('{}', { status: 500 }));
    const failure = await suggestions.dismiss('a');

    expect(failure).not.toBeNull();
    expect(suggestions.for(household, { limit: 2 }).map((item) => item.id)).toEqual(['a', 'b']);
  });

  it('lets the next question be asked again after an undo', async () => {
    serverAnswers(() => answer([suggestion('a')]));
    await suggestions.ask(household, { limit: 1 });

    const fetched = serverAnswers(() => new Response(null, { status: 204 }));
    await suggestions.restore('a');

    serverAnswers(() => answer([suggestion('a')]));
    await suggestions.ask(household, { limit: 1 });

    expect(fetched).toHaveBeenCalledTimes(1);
    expect(suggestions.for(household, { limit: 1 }).map((item) => item.id)).toEqual(['a']);
  });
});

describe('mapping', () => {
  it('carries a reason through, and its subject', async () => {
    serverAnswers(() => answer([suggestion('a', { code: 'ingredient', subject: 'Aubergine' })]));

    await suggestions.ask(household, { limit: 1 });

    expect(suggestions.for(household, { limit: 1 })[0]?.reason).toEqual({
      code: 'ingredient',
      subject: 'Aubergine'
    });
  });

  it('reads a missing reason as none, rather than inventing one', async () => {
    serverAnswers(() => answer([suggestion('a', null)]));

    await suggestions.ask(household, { limit: 1 });

    expect(suggestions.for(household, { limit: 1 })[0]?.reason).toBeNull();
  });
});
