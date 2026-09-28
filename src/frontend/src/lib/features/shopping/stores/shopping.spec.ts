import { afterEach, describe, expect, it, vi } from 'vitest';

import { shopping, type ShoppingItem } from './shopping.svelte';

const json = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' }
  });

const milk: ShoppingItem = {
  itemId: 'i1',
  name: 'Milch',
  quantity: 500,
  unit: 'ml',
  section: 'dairy_eggs',
  isChecked: false,
  isManual: true,
  sources: []
};

afterEach(() => {
  shopping.reset();
  vi.unstubAllGlobals();
});

describe('moving a line to another section', () => {
  it('moves it before the server answers, and rolls it back when the server refuses', async () => {
    let answer: ((response: Response) => void) | null = null;

    vi.stubGlobal(
      'fetch',
      vi.fn(async (input: Request) =>
        input.method === 'GET'
          ? json({ items: [milk] })
          : new Promise<Response>((resolve) => (answer = resolve))
      )
    );

    await shopping.load('h1');

    const moving = shopping.moveToSection('h1', 'i1', 'frozen');

    // The line is already under its new heading while the request is out.
    expect(shopping.toBuy.map((group) => group.section)).toEqual(['frozen']);

    await vi.waitFor(() => expect(answer).not.toBeNull());
    answer!(json({ title: 'Nope', status: 500 }, 500));

    expect(await moving).not.toBeNull();
    expect(shopping.items[0]?.section).toBe('dairy_eggs');
  });
});
