import { beforeEach, describe, expect, it, vi } from 'vitest';

import { sharing } from './sharing.svelte';

/*
 * A share link is a credential for outsiders: pins that a link belongs to one recipe and that
 * taking one back leaves the screen honest.
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
  it('reports a failure to make the link', async () => {
    serverAnswers(() => problem('client.unexpected', 500));

    const failure = await sharing.share(recipeId);

    expect(failure).not.toBeNull();
    expect(sharing.status).toBe('failed');
    expect(sharing.tokenFor(recipeId)).toBeNull();
  });

  it('holds the link only for the recipe it was asked about', async () => {
    serverAnswers(() => json({ token: 'abc', createdAt: '2026-09-18T00:00:00Z' }));

    await sharing.share(recipeId);

    expect(sharing.tokenFor(recipeId)).toBe('abc');
    // One recipe at a time: another recipe's page must not read this one's token from a warm store.
    expect(sharing.tokenFor('r2')).toBeNull();
  });

  it('puts the link back when taking it away fails', async () => {
    serverAnswers((request) =>
      request.method === 'DELETE'
        ? problem('client.unexpected', 500)
        : json({ token: 'abc', createdAt: '2026-09-18T00:00:00Z' })
    );

    await sharing.share(recipeId);
    const failure = await sharing.revoke(recipeId);

    // Telling somebody a link is dead while it is live is the one wrong answer.
    expect(failure).not.toBeNull();
    expect(sharing.tokenFor(recipeId)).toBe('abc');
  });
});
