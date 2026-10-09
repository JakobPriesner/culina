import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { members } from './members.svelte';

const json = (body: unknown) =>
  new Response(JSON.stringify(body), { headers: { 'Content-Type': 'application/json' } });

const person = (userId: string) => ({
  userId,
  displayName: userId,
  role: 'member',
  joinedAt: '2026-01-01T00:00:00Z'
});

beforeEach(() => {
  members.reset();
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('loading members', () => {
  it('keeps the household asked for last when the first answers last', async () => {
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

    const first = members.load('h-flat');
    const second = members.load('h-family');

    await vi.waitFor(() => expect(answers.family).toBeDefined());
    await vi.waitFor(() => expect(answers.flat).toBeDefined());
    answers.family!(json({ items: [person('mum')] }));
    await second;
    answers.flat!(json({ items: [person('flatmate')] }));
    await first;

    expect(members.items.map((one) => one.userId)).toEqual(['mum']);
    expect(members.status).toBe('ready');
  });
});
