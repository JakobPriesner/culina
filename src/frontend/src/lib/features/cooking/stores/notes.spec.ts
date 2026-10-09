import { afterEach, describe, expect, it, vi } from 'vitest';

import { notes } from './notes.svelte';

/* Cook mode reads the note while the recipe page is still sending what was typed; the read must see that write. */
const json = (body: unknown) =>
  new Response(JSON.stringify(body), { headers: { 'Content-Type': 'application/json' } });

afterEach(() => {
  notes.reset();
  vi.unstubAllGlobals();
});

describe('reading a note back', () => {
  it('waits for a save that is still on its way', async () => {
    let stored: string | null = null;
    let arrive: () => void = () => {};
    const sent: string[] = [];

    vi.stubGlobal(
      'fetch',
      vi.fn(async (input: Request) => {
        sent.push(input.method);

        if (input.method === 'PUT') {
          const body = (await input.json()) as { overall: string | null };

          await new Promise<void>((resolve) => (arrive = resolve));
          stored = body.overall;

          return json({ overall: stored, steps: [] });
        }

        return json({ overall: stored, steps: [] });
      })
    );

    await notes.load('r1');
    sent.length = 0;
    notes.set('Use the heavy pan');

    const saving = notes.save('r1');
    const reading = notes.load('r1');

    await vi.waitFor(() => expect(sent).toEqual(['PUT']));

    arrive();
    await Promise.all([saving, reading]);

    expect(sent).toEqual(['PUT', 'GET']);
    expect(notes.overall).toBe('Use the heavy pan');
  });
});

describe('saving a note', () => {
  it('sends nothing for a note that was never read', async () => {
    const fetch = vi.fn(async () => new Response(null, { status: 503 }));

    vi.stubGlobal('fetch', fetch);

    await notes.load('r1');
    fetch.mockClear();

    expect(await notes.save('r1')).toBeNull();
    expect(fetch).not.toHaveBeenCalled();
  });

  it('keeps the step notes it was given', async () => {
    const steps = [{ stepId: 's1', body: 'Lower the heat' }];
    const saved: unknown[] = [];

    vi.stubGlobal(
      'fetch',
      vi.fn(async (input: Request) => {
        if (input.method === 'PUT') {
          saved.push(await input.json());
        }

        return json({ overall: 'Use the heavy pan', steps });
      })
    );

    await notes.load('r1');
    notes.set('Use the cast-iron pan');
    await notes.save('r1');

    expect(saved).toEqual([{ overall: 'Use the cast-iron pan', steps }]);
  });
});

describe('moving from one recipe to the next', () => {
  const answerPerRecipe = () => {
    const answers = new Map<string, (response: Response) => void>();
    const puts: string[] = [];

    vi.stubGlobal(
      'fetch',
      vi.fn(async (input: Request) => {
        if (input.method === 'PUT') {
          puts.push(input.url);

          return json({ overall: null, steps: [] });
        }

        return new Promise<Response>((resolve) => answers.set(input.url, resolve));
      })
    );

    return {
      answers,
      puts,
      answer: (id: string) => [...answers].find(([url]) => url.includes(id))![1]
    };
  };

  it('ends with the note of the recipe asked for last when the earlier answer lands after it', async () => {
    const { answers, answer } = answerPerRecipe();

    const first = notes.load('rA');
    const second = notes.load('rB');

    await vi.waitFor(() => expect(answers.size).toBe(2));
    answer('rB')(json({ overall: 'note B', steps: [] }));
    await second;
    answer('rA')(json({ overall: 'note A', steps: [] }));
    await first;

    expect(notes.overall).toBe('note B');
    expect(notes.loaded).toBe(true);
  });

  it('shows nothing of the previous recipe while the next is read', async () => {
    const { answers, answer } = answerPerRecipe();

    const first = notes.load('rA');

    await vi.waitFor(() => expect(answers.size).toBe(1));
    answer('rA')(json({ overall: 'note A', steps: [] }));
    await first;

    void notes.load('rB');

    expect(notes.overall).toBe('');
    expect(notes.loaded).toBe(false);
  });

  it('does not send typing from one recipe to another', async () => {
    const { answers, answer, puts } = answerPerRecipe();

    const first = notes.load('rA');

    await vi.waitFor(() => expect(answers.size).toBe(1));
    answer('rA')(json({ overall: null, steps: [] }));
    await first;
    notes.set('typed on A');

    void notes.load('rB');

    expect(await notes.save('rB')).toBeNull();
    expect(await notes.save('rA')).toBeNull();
    expect(puts).toEqual([]);
  });
});
