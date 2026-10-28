import { screen, waitFor } from '@testing-library/svelte';
import { userEvent } from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { householdFoods } from '$features/nutrition/stores/householdFoods.svelte';
import { renderWithProviders } from '$lib/test/render';
import { preferences } from '$shell/preferences.svelte';

import DiaryProduct from './DiaryProduct.svelte';

const skyr = '4002971243802';

const json = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': status >= 400 ? 'application/problem+json' : 'application/json' }
  });

const unknown = () => json({ code: 'products.unknown', detail: 'unknown', status: 404 }, 404);

const offered = {
  gtin: skyr,
  name: 'Skyr Natur',
  brand: 'Arla',
  quantity: '450 g',
  packGrams: 450,
  energyKj: 266,
  energyKcal: 63,
  fat: 0.2,
  carbohydrate: 4,
  protein: 11,
  doubts: [],
  url: `https://world.openfoodfacts.org/product/${skyr}`
};

const kept = {
  id: 'f1',
  code: 'h:f1',
  name: 'Arla Skyr Natur',
  energyKj: 266,
  energyKcal: 63,
  own: true,
  gtin: skyr,
  source: 'openFoodFacts'
};

let sent: { method: string; url: string; body: string }[];

function serve(answer: (method: string, url: string) => Response) {
  sent = [];
  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: Request) => {
      sent.push({ method: input.method, url: input.url, body: await input.clone().text() });

      return answer(input.method, input.url);
    })
  );
}

function show(openFoodFacts: boolean) {
  const onfood = vi.fn();

  renderWithProviders(DiaryProduct, {
    props: { householdId: 'h1', openFoodFacts, gtin: skyr, onfood, onback: vi.fn() }
  });

  return onfood;
}

beforeEach(() => {
  preferences.setLocale('en');
  householdFoods.reset();
});

afterEach(() => vi.unstubAllGlobals());

describe('a package into the diary', () => {
  it('goes straight on to the amount when the household kept the package', async () => {
    serve(() => json({ gtin: skyr, name: 'Skyr', householdId: 'h1', version: 1, food: kept }));

    const onfood = show(true);

    await waitFor(() => expect(onfood).toHaveBeenCalledOnce());
    expect(onfood.mock.calls[0]?.[0]).toMatchObject({ code: 'h:f1', householdId: 'h1' });
    expect(sent.some((one) => one.url.includes('/api/v1/products/'))).toBe(false);
  });

  it('shows what Open Food Facts says, with its source, and keeps it in one tap', async () => {
    serve((method, url) =>
      method === 'POST'
        ? json(kept, 201)
        : url.includes(`/api/v1/products/${skyr}`)
          ? json(offered)
          : unknown()
    );

    const onfood = show(true);

    expect(await screen.findByRole('heading', { name: 'Skyr Natur' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /open food facts/i })).toHaveAttribute(
      'href',
      offered.url
    );

    await userEvent.click(screen.getByRole('button', { name: 'Use these values' }));

    await waitFor(() => expect(onfood).toHaveBeenCalledOnce());
    const post = sent.find((one) => one.method === 'POST');
    expect(JSON.parse(post?.body ?? '{}')).toMatchObject({
      name: 'Arla Skyr Natur',
      energyKcal: 63,
      gtin: skyr,
      fromCatalogue: true
    });
  });

  it('asks for the label, and nothing of Open Food Facts, when the server does not look it up', async () => {
    serve(() => unknown());

    show(false);

    expect(
      await screen.findByRole('heading', { name: 'Type it from the package' })
    ).toBeInTheDocument();
    expect(sent.some((one) => one.url.includes('/api/v1/products/'))).toBe(false);
  });
});
