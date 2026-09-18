import { beforeEach, describe, expect, it, vi } from 'vitest';

import { sharing } from './sharing.svelte';

/*
 * The link is a credential handed to people outside the household, so the two
 * things worth pinning are the two that are invisible from the screen: that a
 * missing link reads as "not shared" rather than as a fault, and that taking
 * one back leaves the screen honest whichever way the request goes.
 */
const recipeId = 'r1';

const json = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' }
  });

const problem = (code: string, status: number) =>
  new Response(JSON.stringify({ code, detail: 'No.', status }), {
    status,
    headers: { 'Content-Type': 'application/problem+json' }
  });

function serverAnswers(reply: (request: Request) => Response) {
  vi.stubGlobal(
    'fetch',
    vi.fn((input: Request) => Promise.resolve(reply(input)))
  );
}

beforeEach(() => {
  sharing.reset();
  vi.unstubAllGlobals();
});

describe('sharing', () => {
  it('reads a 404 as "not shared" rather than as a failure', async () => {
    serverAnswers(() => problem('recipes.share_not_found', 404));

    await sharing.load(recipeId);

    // The sheet renders this as its off state and offers to make a link. An
    // error state there would tell somebody something is broken when nothing
    // is.
    expect(sharing.status).toBe('ready');
    expect(sharing.tokenFor(recipeId)).toBeNull();
    expect(sharing.error).toBeNull();
  });

  it('still reports a real failure', async () => {
    serverAnswers(() => problem('client.unexpected', 500));

    await sharing.load(recipeId);

    expect(sharing.status).toBe('failed');
    expect(sharing.error).not.toBeNull();
  });

  it('holds the link only for the recipe it was asked about', async () => {
    serverAnswers(() => json({ token: 'abc', createdAt: '2026-09-18T00:00:00Z' }));

    await sharing.load(recipeId);

    expect(sharing.tokenFor(recipeId)).toBe('abc');
    // One recipe at a time, so another recipe's page must not read this one's
    // token off a store that happens to be warm.
    expect(sharing.tokenFor('r2')).toBeNull();
  });

  it('puts the link back when taking it away fails', async () => {
    serverAnswers((request) =>
      request.method === 'DELETE'
        ? problem('client.unexpected', 500)
        : json({ token: 'abc', createdAt: '2026-09-18T00:00:00Z' })
    );

    await sharing.load(recipeId);
    const failure = await sharing.revoke(recipeId);

    // The optimistic removal has to roll back exactly: telling somebody a link
    // is dead when it is still live is the one wrong answer here.
    expect(failure).not.toBeNull();
    expect(sharing.tokenFor(recipeId)).toBe('abc');
  });
});
