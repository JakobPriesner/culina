import { beforeEach, describe, expect, it, vi } from 'vitest';

import { tags } from './tags.svelte';

const json = (body: unknown) =>
  new Response(JSON.stringify(body), { headers: { 'Content-Type': 'application/json' } });

const tag = (slug: string) => ({ slug, name: slug, recipeCount: 1 });

beforeEach(() => {
  tags.reset();
  vi.unstubAllGlobals();
});

describe('the tags in use', () => {
  it('keeps the household asked for last when the first answers last', async () => {
    const answers: Record<string, (response: Response) => void> = {};

    vi.stubGlobal(
      'fetch',
      vi.fn(
        (input: Request) =>
          new Promise<Response>((resolve) => {
            answers[new URL(input.url).searchParams.get('householdId')!] = resolve;
          })
      )
    );

    const first = tags.load('hA');
    const second = tags.load('hB');

    await vi.waitFor(() => expect(Object.keys(answers)).toHaveLength(2));
    answers.hB!(json({ items: [tag('b')] }));
    await second;
    answers.hA!(json({ items: [tag('a')] }));
    await first;

    expect(tags.items.map((one) => one.slug)).toEqual(['b']);
  });

  it('drops a read that lands after sign-out', async () => {
    const held = Promise.withResolvers<Response>();

    vi.stubGlobal(
      'fetch',
      vi.fn(() => held.promise)
    );

    const loading = tags.load('h1');

    tags.reset();
    held.resolve(json({ items: [tag('a')] }));
    await loading;

    expect(tags.items).toEqual([]);
  });
});
