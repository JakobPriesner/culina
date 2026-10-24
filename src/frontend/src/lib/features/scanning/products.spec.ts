import { afterEach, describe, expect, it, vi } from 'vitest';

import { lookupProduct, nameProduct } from './products';

const problem = (status: number, code: string) =>
  new Response(JSON.stringify({ code, detail: code, status }), {
    status,
    headers: { 'Content-Type': 'application/problem+json' }
  });

const json = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });

const barilla = {
  gtin: '8076800195057',
  name: 'Barilla Spaghetti n.5',
  ingredient: 'Spaghetti',
  cookMinutes: 9,
  householdId: 'h1',
  version: 1
};

afterEach(() => vi.unstubAllGlobals());

describe('the household’s memory of a barcode', () => {
  it('reads a known barcode, absent fields as null', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => json(barilla))
    );

    const result = await lookupProduct('h1', '8076800195057');

    expect(result.ok && result.value).toEqual({
      gtin: '8076800195057',
      name: 'Barilla Spaghetti n.5',
      ingredient: 'Spaghetti',
      quantity: null,
      unit: null,
      cookMinutes: 9,
      note: null,
      householdId: 'h1',
      version: 1
    });
  });

  it('answers null for a barcode nobody named: that is an answer, not a failure', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => problem(404, 'products.unknown'))
    );

    const result = await lookupProduct('h1', '8076800195057');

    expect(result).toEqual({ ok: true, value: null });
  });

  it('keeps any other failure a failure', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => problem(404, 'households.not_found'))
    );

    const result = await lookupProduct('h1', '8076800195057');

    expect(result.ok).toBe(false);
  });

  it('names a barcode with one text, kept as both the name and the ingredient it counts as', async () => {
    const sent: { method: string; url: string; body: unknown }[] = [];

    vi.stubGlobal(
      'fetch',
      vi.fn(async (input: Request) => {
        sent.push({ method: input.method, url: input.url, body: await input.clone().json() });

        return json({ ...barilla, name: 'Spaghetti' }, 201);
      })
    );

    const result = await nameProduct('h1', '8076800195057', '  Spaghetti ');

    expect(sent).toHaveLength(1);
    expect(sent[0]).toMatchObject({
      method: 'PUT',
      body: { name: 'Spaghetti', ingredient: 'Spaghetti' }
    });
    expect(sent[0]!.url).toMatch(/\/api\/v1\/households\/h1\/products\/8076800195057$/);
    expect(result.ok && result.value.name).toBe('Spaghetti');
  });
});
