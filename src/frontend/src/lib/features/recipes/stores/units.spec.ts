import { beforeEach, describe, expect, it, vi } from 'vitest';

import { units } from './units.svelte';

const json = (body: unknown) =>
  new Response(JSON.stringify(body), { headers: { 'Content-Type': 'application/json' } });

beforeEach(() => {
  units.reset();
  vi.unstubAllGlobals();
});

describe('the household’s own units', () => {
  it('keeps a unit remembered while the read was out', async () => {
    const held = Promise.withResolvers<Response>();

    vi.stubGlobal(
      'fetch',
      vi.fn(() => held.promise)
    );

    const loading = units.load('h1');

    units.remember('Schuss');
    held.resolve(json({ own: ['Dose'] }));
    await loading;

    expect(units.own).toEqual(['Dose', 'Schuss']);
  });

  it('does not list a unit twice when the server already knows it', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(() => Promise.resolve(json({ own: ['Dose'] })))
    );
    await units.load('h1');

    expect(units.own).toEqual(['Dose']);
  });

  it('drops the answer for the household that was left', async () => {
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

    const first = units.load('hA');
    const second = units.load('hB');

    await vi.waitFor(() => expect(Object.keys(answers)).toHaveLength(2));
    answers.b!(json({ own: ['B-Einheit'] }));
    await second;
    answers.a!(json({ own: ['A-Einheit'] }));
    await first;

    expect(units.own).toEqual(['B-Einheit']);
  });

  it('drops a read that lands after sign-out', async () => {
    const held = Promise.withResolvers<Response>();

    vi.stubGlobal(
      'fetch',
      vi.fn(() => held.promise)
    );

    const loading = units.load('h1');

    units.reset();
    held.resolve(json({ own: ['Dose'] }));
    await loading;

    expect(units.own).toEqual([]);
  });
});
