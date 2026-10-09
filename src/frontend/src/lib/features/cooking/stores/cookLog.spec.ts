import { afterEach, describe, expect, it, vi } from 'vitest';

import { cookLog } from './cookLog.svelte';

const json = (body: unknown) =>
  new Response(JSON.stringify(body), { headers: { 'Content-Type': 'application/json' } });

const log = (count: number) => ({ count, lastMadeAt: null, items: [] });

afterEach(() => {
  cookLog.reset();
  vi.unstubAllGlobals();
});

describe('moving from one recipe to the next', () => {
  const answerPerRecipe = () => {
    const answers = new Map<string, (response: Response) => void>();

    vi.stubGlobal(
      'fetch',
      vi.fn((input: Request) => new Promise<Response>((resolve) => answers.set(input.url, resolve)))
    );

    return { answers, answer: (id: string) => [...answers].find(([url]) => url.includes(id))![1] };
  };

  it('ends with the log of the recipe asked for last when the earlier answer lands after it', async () => {
    const { answers, answer } = answerPerRecipe();

    const first = cookLog.load('rA');
    const second = cookLog.load('rB');

    await vi.waitFor(() => expect(answers.size).toBe(2));
    answer('rB')(json(log(2)));
    await second;
    answer('rA')(json(log(7)));
    await first;

    expect(cookLog.count).toBe(2);
  });

  it('shows nothing of the previous recipe while the next is read', async () => {
    const { answers, answer } = answerPerRecipe();

    const first = cookLog.load('rA');

    await vi.waitFor(() => expect(answers.size).toBe(1));
    answer('rA')(json(log(7)));
    await first;
    expect(cookLog.count).toBe(7);

    void cookLog.load('rB');

    expect(cookLog.count).toBe(0);
  });

  it('drops a photo answer for the recipe that was left', async () => {
    const { answers, answer } = answerPerRecipe();

    const first = cookLog.load('rA');

    await vi.waitFor(() => expect(answers.size).toBe(1));
    answer('rA')(json(log(7)));
    await first;

    const photo = cookLog.removePhoto('rA', 'e1');

    void cookLog.load('rB');
    await vi.waitFor(() => expect(answers.size).toBe(3));
    [...answers].find(([url]) => url.endsWith('/photo'))![1](json(log(8)));
    await photo;

    expect(cookLog.count).toBe(0);
  });
});
