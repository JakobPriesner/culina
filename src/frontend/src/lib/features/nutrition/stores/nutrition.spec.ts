import { afterEach, describe, expect, it, vi } from 'vitest';

import { nutrition } from './nutrition.svelte';

/* A revisit shows what it saw while it asks again, and nothing from one recipe or household is shown for another. */
const answer = (kcal: number) => ({
  per: 'serving',
  yield: 2,
  complete: true,
  counted: 1,
  lines: 1,
  values: Object.fromEntries(
    [
      'energyKj',
      'energyKcal',
      'fat',
      'saturatedFat',
      'carbohydrate',
      'sugars',
      'protein',
      'salt'
    ].map((name) => [name, { value: kcal, atLeast: false }])
  ),
  ingredients: [],
  source: { name: 'BLS', version: '4.0', publisher: 'MRI', licence: 'CC BY 4.0' }
});

const json = (body: unknown) =>
  new Response(JSON.stringify(body), { headers: { 'Content-Type': 'application/json' } });

afterEach(() => {
  nutrition.reset();
  vi.unstubAllGlobals();
});

describe('the nutrition store', () => {
  it('keeps one answer per recipe and household', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn((input: Request) =>
        Promise.resolve(json(answer(new URL(input.url).searchParams.has('householdId') ? 100 : 1)))
      )
    );

    await nutrition.load('r1', 'h1');

    expect(nutrition.answerFor('r1', 'h1')?.values.energyKcal.value).toBe(100);
    expect(nutrition.answerFor('r2', 'h1')).toBeNull();
    expect(nutrition.answerFor('r1', 'h2')).toBeNull();
    expect(nutrition.statusFor('r2', 'h1')).toBe('idle');
  });

  it('shows what it saw at once on a revisit, then what the server now says', async () => {
    const answers = [answer(100), answer(200)];
    let release: (() => void) | undefined;

    vi.stubGlobal(
      'fetch',
      vi.fn(async () => {
        const body = answers.shift()!;

        if (body.values['energyKcal']?.value === 200) {
          await new Promise<void>((resume) => (release = resume));
        }

        return json(body);
      })
    );

    await nutrition.load('r1', 'h1');

    const second = nutrition.load('r1', 'h1');

    expect(nutrition.statusFor('r1', 'h1')).toBe('ready');
    expect(nutrition.answerFor('r1', 'h1')?.values.energyKcal.value).toBe(100);

    await vi.waitFor(() => expect(release).toBeDefined());
    release?.();
    await second;

    expect(nutrition.answerFor('r1', 'h1')?.values.energyKcal.value).toBe(200);
  });

  it('keeps showing a known answer when asking again fails, and fails only when it has none', async () => {
    const replies = [() => json(answer(100)), () => new Response(null, { status: 503 })];

    vi.stubGlobal(
      'fetch',
      vi.fn(() => Promise.resolve(replies.shift()!()))
    );

    await nutrition.load('r1', 'h1');
    await nutrition.load('r1', 'h1');

    expect(nutrition.statusFor('r1', 'h1')).toBe('ready');
    expect(nutrition.answerFor('r1', 'h1')?.values.energyKcal.value).toBe(100);

    vi.stubGlobal(
      'fetch',
      vi.fn(() => Promise.resolve(new Response(null, { status: 503 })))
    );

    await nutrition.load('r2', 'h1');

    expect(nutrition.statusFor('r2', 'h1')).toBe('failed');
  });

  it('drops an answer that arrives after the reader has moved on', async () => {
    const pending: ((response: Response) => void)[] = [];

    vi.stubGlobal(
      'fetch',
      vi.fn(() => new Promise<Response>((resolve) => pending.push(resolve)))
    );

    const first = nutrition.load('r1', 'h1');
    const second = nutrition.load('r2', 'h1');

    await vi.waitFor(() => expect(pending).toHaveLength(2));

    pending[1]?.(json(answer(2)));
    await second;
    pending[0]?.(json(answer(1)));
    await first;

    expect(nutrition.answerFor('r2', 'h1')?.values.energyKcal.value).toBe(2);
    expect(nutrition.answerFor('r1', 'h1')).toBeNull();
  });
});
