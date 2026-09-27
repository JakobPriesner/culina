import { afterEach, describe, expect, it, vi } from 'vitest';

import { notes } from './notes.svelte';

/*
 * A note is read back the moment cook mode opens, while the recipe page that
 * was just left is still sending what was typed on it. The read must see that
 * write, or cook mode shows the note as it was before.
 */
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
