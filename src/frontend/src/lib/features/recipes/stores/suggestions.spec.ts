import { beforeEach, describe, expect, it, vi } from 'vitest';

import { suggestions } from './suggestions.svelte';

/*
 * The store components read suggestions from. The first test matters most: the store is called from
 * an `$effect`, so a `$state` guard would loop into a flood of identical requests and a 429.
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
    // A guard that re-armed itself on failure would be the same loop.
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

describe('remembering', () => {
  const week = (planned: number) => ({ limit: 1, exclude: [`recipe-${planned}`] });

  it('forgets the question used least recently once too many are kept', async () => {
    serverAnswers(() => answer([suggestion('a')]));

    for (let planned = 0; planned < 21; planned += 1) {
      await suggestions.ask(household, week(planned));
    }

    expect(suggestions.statusOf(household, week(0))).toBe('idle');
    expect(suggestions.for(household, week(0))).toEqual([]);
    expect(suggestions.statusOf(household, week(1))).toBe('ready');
    expect(suggestions.statusOf(household, week(20))).toBe('ready');
  });

  it('asks a forgotten question again when it comes back', async () => {
    const fetched = serverAnswers(() => answer([suggestion('a')]));

    for (let planned = 0; planned < 21; planned += 1) {
      await suggestions.ask(household, week(planned));
    }

    await suggestions.ask(household, week(0));

    expect(fetched).toHaveBeenCalledTimes(22);
    expect(suggestions.statusOf(household, week(0))).toBe('ready');
  });

  it('keeps the question being looked at however many others come after it', async () => {
    serverAnswers(() => answer([suggestion('a')]));

    await suggestions.ask(household, week(0));

    for (let planned = 1; planned < 30; planned += 1) {
      suggestions.for(household, week(0));
      await suggestions.ask(household, week(planned));
    }

    expect(suggestions.statusOf(household, week(0))).toBe('ready');
  });

  it('does not ask a question that is still remembered twice', async () => {
    const fetched = serverAnswers(() => answer([suggestion('a')]));

    for (let planned = 0; planned < 20; planned += 1) {
      await suggestions.ask(household, week(planned));
    }

    await suggestions.ask(household, week(0));

    expect(fetched).toHaveBeenCalledTimes(20);
  });
});

describe('dismissing', () => {
  it('removes the card from every answer at once, not just the one on screen', async () => {
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

describe('a deleted recipe', () => {
  it('is taken out of every answer, without asking the server anything', async () => {
    serverAnswers(() => answer([suggestion('a'), suggestion('b')]));

    await suggestions.ask(household, { limit: 2 });
    await suggestions.ask(household, { limit: 2, slot: 'dinner' });

    const fetched = serverAnswers(() => answer([]));
    suggestions.forget('a');

    expect(suggestions.for(household, { limit: 2 }).map((item) => item.id)).toEqual(['b']);
    expect(suggestions.for(household, { limit: 2, slot: 'dinner' }).map((item) => item.id)).toEqual(
      ['b']
    );
    expect(fetched).not.toHaveBeenCalled();
  });
});

describe('the end of the shortlist', () => {
  const kitchen = ['a', 'b', 'c', 'd', 'e', 'f', 'g', 'h'];

  const ranked = (url: string) => {
    const query = new URL(url).searchParams;
    const excluded = query.getAll('exclude');
    const limit = Number(query.get('limit') ?? 5);

    return answer(
      kitchen
        .filter((id) => !excluded.includes(id))
        .slice(0, limit)
        .map((id) => suggestion(id))
    );
  };

  it('asks for the next few by naming the ones already shown', async () => {
    const fetched = serverAnswers(ranked);

    await suggestions.ask(household, { limit: 3 });
    await suggestions.more(household, { limit: 3 });

    expect(suggestions.for(household, { limit: 3 }).map((one) => one.id)).toEqual([
      'a',
      'b',
      'c',
      'd',
      'e',
      'f'
    ]);
    expect(new URL(fetched.mock.calls[1]?.[0].url ?? '').searchParams.getAll('exclude')).toEqual([
      'a',
      'b',
      'c'
    ]);
  });

  it('stops once an answer comes back short', async () => {
    const fetched = serverAnswers(ranked);

    await suggestions.ask(household, { limit: 3 });
    await suggestions.more(household, { limit: 3 });
    await suggestions.more(household, { limit: 3 });

    expect(suggestions.for(household, { limit: 3 })).toHaveLength(8);
    expect(suggestions.hasMore(household, { limit: 3 })).toBe(false);

    await suggestions.more(household, { limit: 3 });

    expect(fetched).toHaveBeenCalledTimes(3);
  });

  it('has nothing more to ask for when the first answer was already short', async () => {
    serverAnswers(ranked);

    await suggestions.ask(household, { limit: 12 });

    expect(suggestions.hasMore(household, { limit: 12 })).toBe(false);
  });

  it('asks once for a page, however often the end is reached', async () => {
    const fetched = serverAnswers(ranked);

    await suggestions.ask(household, { limit: 3 });
    await Promise.all([
      suggestions.more(household, { limit: 3 }),
      suggestions.more(household, { limit: 3 })
    ]);

    expect(fetched).toHaveBeenCalledTimes(2);
  });
});
