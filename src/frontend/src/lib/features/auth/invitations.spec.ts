import { beforeEach, describe, expect, it, vi } from 'vitest';

import { invitations } from './invitations.svelte';

/*
 * A link on screen is a way into one particular kitchen. Shown under another
 * household's name, whoever it is sent to joins the wrong one.
 */
const json = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' }
  });

beforeEach(() => {
  invitations.reset();
  vi.stubGlobal(
    'fetch',
    vi.fn((input: Request) =>
      Promise.resolve(
        input.method === 'POST'
          ? json(
              { invitationId: 'i1', code: 'secret-code', expiresAt: '2026-10-10T00:00:00Z' },
              201
            )
          : json({ items: [] })
      )
    )
  );
});

describe('the invitation just made', () => {
  it('is shown for the household it was made for', async () => {
    await invitations.load('h-flat');
    await invitations.create('h-flat');

    expect(invitations.freshCode).toBe('secret-code');
  });

  it('is shown when it is the first thing read for a household', async () => {
    await invitations.create('h-flat');

    expect(invitations.freshCode).toBe('secret-code');
  });

  it('is not shown once another household is looked at', async () => {
    await invitations.load('h-flat');
    await invitations.create('h-flat');

    await invitations.load('h-family');

    expect(invitations.freshCode).toBeNull();
  });
});
