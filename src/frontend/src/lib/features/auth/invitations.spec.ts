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

describe('answers that arrive after a switch', () => {
  it('does not show a link made for the household that was left', async () => {
    let answer: ((response: Response) => void) | null = null;

    vi.stubGlobal(
      'fetch',
      vi.fn((input: Request) =>
        input.method === 'POST'
          ? new Promise<Response>((resolve) => (answer = resolve))
          : Promise.resolve(json({ items: [] }))
      )
    );

    const creating = invitations.create('h-flat');
    await invitations.load('h-family');

    await vi.waitFor(() => expect(answer).not.toBeNull());
    answer!(
      json({ invitationId: 'i1', code: 'secret-code', expiresAt: '2026-10-10T00:00:00Z' }, 201)
    );
    await creating;

    expect(invitations.freshCode).toBeNull();
  });

  it('keeps the list of the household asked for last when the first answers last', async () => {
    const answers: Record<string, (response: Response) => void> = {};

    vi.stubGlobal(
      'fetch',
      vi.fn(
        (input: Request) =>
          new Promise<Response>((resolve) => {
            answers[input.url.includes('h-flat') ? 'flat' : 'family'] = resolve;
          })
      )
    );

    const first = invitations.load('h-flat');
    const second = invitations.load('h-family');

    await vi.waitFor(() => expect(answers.family).toBeDefined());
    await vi.waitFor(() => expect(answers.flat).toBeDefined());
    answers.family!(json({ items: [{ invitationId: 'f', createdAt: 'x', expiresAt: 'y' }] }));
    await second;
    answers.flat!(json({ items: [{ invitationId: 'a', createdAt: 'x', expiresAt: 'y' }] }));
    await first;

    expect(invitations.items.map((one) => one.invitationId)).toEqual(['f']);
  });
});
