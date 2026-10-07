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
