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

describe('a correction', () => {
  const line = (ingredientId: string, name: string) => ({
    ingredientId,
    status: 'counted',
    food: { code: 'M110100', nameDe: 'Butter', nameEn: 'Butter' },
    grams: 100,
    via: 'mass',
    corrected: false,
    energyKcal: 700,
    name
  });

  const withLines = (lines: unknown[]) => ({ ...answer(700), ingredients: lines });

  const sweet = { code: 'M111111', nameDe: 'Süßrahmbutter', nameEn: 'Sweet cream butter' };

  it('changes the rows at once, tells the server by the name as written, and reads the answer again', async () => {
    const calls: { method: string; url: string; body: string }[] = [];
    let release: (() => void) | undefined;

    vi.stubGlobal(
      'fetch',
      vi.fn(async (input: Request) => {
        calls.push({ method: input.method, url: input.url, body: await input.clone().text() });

        if (input.method === 'PUT') {
          await new Promise<void>((resume) => (release = resume));

          return new Response(null, { status: 204 });
        }

        return json(withLines([line('a', 'Butter')]));
      })
    );

    await nutrition.load('r1', 'h1');

    const done = nutrition.correct('r1', 'h1', 'Müsli & Nüsse/Mix', ['a'], {
      kind: 'food',
      food: sweet
    });

    await vi.waitFor(() => expect(release).toBeDefined());

    const shown = nutrition.answerFor('r1', 'h1')?.ingredients[0];

    expect(shown?.food?.nameEn).toBe('Sweet cream butter');
    expect(shown?.corrected).toBe(true);

    release?.();

    expect(await done).toBeNull();

    const put = calls.find((call) => call.method === 'PUT')!;

    expect(put.url).toContain('/households/h1/ingredients/M%C3%BCsli%20%26%20N%C3%BCsse%2FMix');
    expect(JSON.parse(put.body)).toEqual({ food: 'M111111' });
    expect(calls.filter((call) => call.method === 'GET')).toHaveLength(2);
  });

  it('moves a line to not counted, and sends null', async () => {
    let body = '';

    vi.stubGlobal(
      'fetch',
      vi.fn(async (input: Request) => {
        if (input.method === 'PUT') {
          body = await input.text();

          return new Response(null, { status: 204 });
        }

        return json(withLines([line('a', 'Butter')]));
      })
    );

    await nutrition.load('r1', 'h1');
    await nutrition.correct('r1', 'h1', 'Butter', ['a'], { kind: 'exclude' });

    expect(JSON.parse(body)).toEqual({ food: null });
  });

  it('puts the rows back when the server refuses, and hands the failure over', async () => {
    let release: (() => void) | undefined;

    vi.stubGlobal(
      'fetch',
      vi.fn(async (input: Request) => {
        if (input.method === 'PUT') {
          await new Promise<void>((resume) => (release = resume));

          return new Response(
            JSON.stringify({ code: 'nutrition.unknown_food', title: 'x', status: 400 }),
            { status: 400, headers: { 'Content-Type': 'application/problem+json' } }
          );
        }

        return json(withLines([line('a', 'Butter')]));
      })
    );

    await nutrition.load('r1', 'h1');

    const done = nutrition.correct('r1', 'h1', 'Butter', ['a'], { kind: 'exclude' });

    await vi.waitFor(() => expect(release).toBeDefined());
    expect(nutrition.answerFor('r1', 'h1')?.ingredients[0]?.status).toBe('excluded');

    release?.();

    const failure = await done;

    expect(failure).not.toBeNull();
    expect(nutrition.answerFor('r1', 'h1')?.ingredients[0]?.status).toBe('counted');
    expect(nutrition.answerFor('r1', 'h1')?.ingredients[0]?.corrected).toBe(false);
  });

  it('puts the rows back when the network is down', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async (input: Request) => {
        if (input.method === 'DELETE') {
          throw new TypeError('offline');
        }

        return json(withLines([{ ...line('a', 'Butter'), corrected: true }]));
      })
    );

    await nutrition.load('r1', 'h1');

    const failure = await nutrition.correct('r1', 'h1', 'Butter', ['a'], { kind: 'default' });

    expect(failure).not.toBeNull();
    expect(nutrition.answerFor('r1', 'h1')?.ingredients[0]?.corrected).toBe(true);
  });
});

describe('an answer this client cannot read', () => {
  it('is a failed read, not an unhandled error', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(() => Promise.resolve(json({ items: [] })))
    );

    await nutrition.load('r1', 'h1');

    expect(nutrition.statusFor('r1', 'h1')).toBe('failed');
  });
});
