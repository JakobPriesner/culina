import { beforeEach, describe, expect, it, vi } from 'vitest';

import { householdFoods } from './householdFoods.svelte';

const json = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' }
  });

const food = (id: string, name: string) => ({
  id,
  code: id,
  name,
  energyKj: 100,
  energyKcal: 24,
  own: true
});

beforeEach(() => {
  householdFoods.reset();
  vi.unstubAllGlobals();
});

describe('the household’s foods', () => {
  it('drops a read for the household that was left', async () => {
    const answers: Record<string, (response: Response) => void> = {};

    vi.stubGlobal(
      'fetch',
      vi.fn(
        (input: Request) =>
          new Promise<Response>((resolve) => {
            answers[input.url.includes('hA') ? 'a' : 'b'] = resolve;
          })
      )
    );

    const first = householdFoods.load('hA');
    const second = householdFoods.load('hB');

    await vi.waitFor(() => expect(Object.keys(answers)).toHaveLength(2));
    answers.b!(json({ items: [food('b', 'B')] }));
    await second;
    answers.a!(json({ items: [food('a', 'A')] }));
    await first;

    expect(householdFoods.foods.map((one) => one.id)).toEqual(['b']);
  });

  async function removingWhileOut() {
    vi.stubGlobal(
      'fetch',
      vi.fn(() => Promise.resolve(json({ items: [food('a', 'A'), food('b', 'B')] })))
    );
    await householdFoods.load('h1');

    const held = Promise.withResolvers<Response>();
    const refuse = () => held.resolve(json({ status: 500 }, 500));

    vi.stubGlobal(
      'fetch',
      vi.fn((input: Request) =>
        input.method === 'DELETE' ? held.promise : Promise.resolve(json(food('c', 'C'), 201))
      )
    );

    return { removing: householdFoods.remove('h1', 'a'), refuse: () => refuse() };
  }

  it('puts back only the removed food, keeping one added meanwhile', async () => {
    const { removing, refuse } = await removingWhileOut();

    await vi.waitFor(() => expect(householdFoods.foods).toHaveLength(1));
    await householdFoods.add('h1', { name: 'C', energyKj: null, energyKcal: 24 } as never);
    refuse();
    await removing;

    expect(householdFoods.foods.map((one) => one.id)).toEqual(['a', 'b', 'c']);
  });

  it('does not refill the store after sign-out', async () => {
    const { removing, refuse } = await removingWhileOut();

    householdFoods.reset();
    refuse();
    await removing;

    expect(householdFoods.foods).toEqual([]);
  });
});
