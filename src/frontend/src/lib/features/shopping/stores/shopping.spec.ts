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

describe('ticking lines quickly', () => {
  const eggs: ShoppingItem = { ...milk, itemId: 'i2', name: 'Eier' };

  it('keeps a tick that succeeded when an earlier one fails afterwards', async () => {
    const answers: ((response: Response) => void)[] = [];

    vi.stubGlobal(
      'fetch',
      vi.fn(async (input: Request) =>
        input.method === 'GET'
          ? json({ items: [milk, eggs] })
          : new Promise<Response>((resolve) => answers.push(resolve))
      )
    );

    await shopping.load('h1');

    const first = shopping.check('h1', 'i1', true);
    const second = shopping.check('h1', 'i2', true);

    await vi.waitFor(() => expect(answers).toHaveLength(2));
    answers[1]!(
      json({
        items: [
          { ...milk, isChecked: true },
          { ...eggs, isChecked: true }
        ]
      })
    );
    await second;
    answers[0]!(json({ title: 'Nope', status: 500 }, 500));
    await first;

    expect(shopping.items.map((item) => item.isChecked)).toEqual([false, true]);
  });

  it('keeps both ticks when the newer one is answered first, taking the last answer', async () => {
    const answers: ((response: Response) => void)[] = [];

    vi.stubGlobal(
      'fetch',
      vi.fn(async (input: Request) =>
        input.method === 'GET'
          ? json({ items: [milk, eggs] })
          : new Promise<Response>((resolve) => answers.push(resolve))
      )
    );

    await shopping.load('h1');

    const first = shopping.check('h1', 'i1', true);
    const second = shopping.check('h1', 'i2', true);

    await vi.waitFor(() => expect(answers).toHaveLength(2));
    // The server handled the second tick first, so its list still shows milk unticked.
    answers[1]!(json({ items: [milk, { ...eggs, isChecked: true }] }));
    await second;
    answers[0]!(
      json({
        items: [
          { ...milk, isChecked: true },
          { ...eggs, isChecked: true }
        ]
      })
    );
    await first;

    expect(shopping.items.map((item) => item.isChecked)).toEqual([true, true]);
  });
});

describe('answers that arrive after a household switch', () => {
  const answers: Record<string, (response: Response) => void> = {};

  const stubSlowReads = () =>
    vi.stubGlobal(
      'fetch',
      vi.fn(
        (input: Request) =>
          new Promise<Response>((resolve) => {
            answers[input.url.includes('h1') ? 'h1' : 'h2'] = resolve;
          })
      )
    );

  it('keeps the list of the household asked for last when the first answers last', async () => {
    stubSlowReads();

    const first = shopping.load('h1');
    const second = shopping.load('h2');

    await vi.waitFor(() => expect(answers.h2).toBeDefined());
    await vi.waitFor(() => expect(answers.h1).toBeDefined());
    answers.h2!(json({ items: [{ ...milk, itemId: 'b', name: 'Brot' }] }));
    await second;
    answers.h1!(json({ items: [milk] }));
    await first;

    expect(shopping.items.map((item) => item.name)).toEqual(['Brot']);
  });

  it('does not show a list that a write for the household left came back with', async () => {
    stubSlowReads();

    const adding = shopping.add('h1', 'Milch');
    const loading = shopping.load('h2');

    await vi.waitFor(() => expect(answers.h2).toBeDefined());
    await vi.waitFor(() => expect(answers.h1).toBeDefined());
    answers.h2!(json({ items: [] }));
    await loading;
    answers.h1!(json({ items: [milk] }));
    await adding;

    expect(shopping.items).toEqual([]);
  });
});

describe('a tick then an untick that both fail', () => {
  it('leaves the line as the server still has it', async () => {
    const answers: ((response: Response) => void)[] = [];

    vi.stubGlobal(
      'fetch',
      vi.fn(async (input: Request) =>
        input.method === 'GET'
          ? json({ items: [milk] })
          : new Promise<Response>((resolve) => answers.push(resolve))
      )
    );

    await shopping.load('h1');

    const tick = shopping.check('h1', 'i1', true);
    const untick = shopping.check('h1', 'i1', false);

    await vi.waitFor(() => expect(answers).toHaveLength(2));
    answers[0]!(json({ title: 'Offline', status: 500 }, 500));
    await tick;
    answers[1]!(json({ title: 'Offline', status: 500 }, 500));
    await untick;

    expect(shopping.items[0]?.isChecked).toBe(false);
  });

  it('restores the tick the server did confirm when only the untick fails', async () => {
    const answers: ((response: Response) => void)[] = [];

    vi.stubGlobal(
      'fetch',
      vi.fn(async (input: Request) =>
        input.method === 'GET'
          ? json({ items: [milk] })
          : new Promise<Response>((resolve) => answers.push(resolve))
      )
    );

    await shopping.load('h1');

    const tick = shopping.check('h1', 'i1', true);
    const untick = shopping.check('h1', 'i1', false);

    await vi.waitFor(() => expect(answers).toHaveLength(2));
    answers[0]!(json({ items: [{ ...milk, isChecked: true }] }));
    await tick;
    answers[1]!(json({ title: 'Offline', status: 500 }, 500));
    await untick;

    expect(shopping.items[0]?.isChecked).toBe(true);
  });
});

describe('lines a plan puts on the list', () => {
  it('are marked as arrived for a moment, with lines whose amount changed, and nothing else', async () => {
    vi.useFakeTimers();
    const flour: ShoppingItem = { ...milk, itemId: 'i3', name: 'Mehl', quantity: 200, unit: 'g' };
    const salt: ShoppingItem = { ...milk, itemId: 'i4', name: 'Salz', quantity: null, unit: null };

    vi.stubGlobal(
      'fetch',
      vi.fn(async (input: Request) =>
        input.method === 'GET'
          ? json({ items: [milk, salt] })
          : json({ items: [{ ...milk, quantity: 750 }, salt, flour] })
      )
    );

    await shopping.load('h1');
    expect(shopping.arrived).toEqual([]);

    await shopping.addPlannedWeek('h1', '2026-10-19');
    expect(shopping.arrived).toEqual(['i1', 'i3']);

    vi.advanceTimersByTime(2400);
    expect(shopping.arrived).toEqual([]);
    vi.useRealTimers();
  });
});
