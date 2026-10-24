import { afterEach, describe, expect, it, vi } from 'vitest';

import { diary } from './diary.svelte';

const json = (body: unknown) =>
  new Response(JSON.stringify(body), { headers: { 'Content-Type': 'application/json' } });

/** A day with something logged on it, as the server answers it. */
const loggedDay = (date: string) => ({
  date,
  entries: [
    {
      entryId: 'e-1',
      date,
      meal: 'lunch',
      name: 'Linsen-Dal',
      amount: 1,
      unit: 'portion',
      nutrition: { kcal: 520 }
    }
  ],
  totals: { kcal: 520 },
  atLeast: false,
  estimated: false,
  typedKcal: 0
});

describe('reading a day of the diary from an effect', () => {
  afterEach(() => {
    diary.reset();
    vi.unstubAllGlobals();
  });

  it('asks once, and not again for every answer that holds the day', async () => {
    const fetch = vi.fn(async () => json({ days: [loggedDay('2026-10-24')] }));
    vi.stubGlobal('fetch', fetch);

    const stop = $effect.root(() => {
      $effect(() => {
        void diary.load('2026-10-24');
      });
    });

    await vi.waitFor(() => expect(diary.day('2026-10-24')).not.toBeNull());
    await new Promise((resolve) => setTimeout(resolve, 50));

    expect(fetch).toHaveBeenCalledTimes(1);
    stop();
  });
});
