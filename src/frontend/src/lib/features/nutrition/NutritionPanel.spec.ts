import { screen, waitFor, within } from '@testing-library/svelte';
import { userEvent } from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import NutritionPanel from './NutritionPanel.svelte';
import { nutrition } from './stores/nutrition.svelte';
import type { Recipe } from '$features/recipes/types';
import { renderWithProviders } from '$lib/test/render';
import { preferences } from '$shell/preferences.svelte';

const butter = 'i-butter';
const onion = 'i-onion';
const salt = 'i-salt';
const oil = 'i-oil';
const egg = 'i-egg';

const recipe: Recipe = {
  id: 'r1',
  householdId: 'h1',
  title: 'Onion butter',
  description: null,
  language: 'en',
  yieldAmount: 2,
  yieldKind: 'servings',
  yieldLabel: null,
  prepMinutes: null,
  cookMinutes: null,
  totalMinutes: null,
  imageId: null,
  tags: [],
  sourceUrl: null,
  createdBy: 'u1',
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
  version: 1,
  steps: [],
  groups: [
    {
      id: 'g1',
      name: null,
      ingredients: [
        { id: butter, quantity: { value: 200, unit: 'g' }, name: 'butter', note: null },
        { id: onion, quantity: { value: 1, unit: 'piece' }, name: 'onion', note: null },
        { id: salt, quantity: { value: null, unit: null }, name: 'salt', note: null },
        { id: oil, quantity: { value: 2, unit: 'tbsp' }, name: 'olive oil', note: null },
        { id: egg, quantity: { value: 1, unit: 'piece' }, name: 'egg', note: null }
      ]
    }
  ]
};

const value = (amount: number, atLeast = false) => ({ value: amount, atLeast });

const source = {
  name: 'Bundeslebensmittelschlüssel',
  version: '4.0',
  publisher: 'Max Rubner-Institut',
  licence: 'CC BY 4.0'
};

const values = (energyKcal: number, atLeast: boolean) => ({
  energyKj: value(energyKcal * 4.184, atLeast),
  energyKcal: value(energyKcal, atLeast),
  fat: value(31.62, atLeast),
  saturatedFat: value(19.04, atLeast),
  carbohydrate: value(7.46, atLeast),
  sugars: value(3.05, atLeast),
  protein: value(9.2, atLeast),
  salt: value(0.456, atLeast)
});

/** Butter and an egg counted, an onion by density, the rest not. */
const partial = (energyKcal = 520.9) => ({
  per: 'serving',
  yield: 2,
  complete: false,
  counted: 3,
  lines: 5,
  values: values(energyKcal, true),
  ingredients: [
    {
      ingredientId: butter,
      status: 'counted',
      food: { code: 'M110100', nameDe: 'Butter', nameEn: 'Butter' },
      grams: 200,
      via: 'mass',
      corrected: false,
      energyKcal: 372.4
    },
    {
      ingredientId: onion,
      status: 'counted',
      food: { code: 'G410100', nameDe: 'Zwiebel, roh', nameEn: 'Onion, raw' },
      grams: 27.4,
      via: 'density',
      corrected: false,
      energyKcal: 5.6
    },
    { ingredientId: salt, status: 'noAmount', corrected: false },
    { ingredientId: oil, status: 'amountNotInGrams', corrected: false },
    {
      ingredientId: egg,
      status: 'counted',
      food: { code: 'E110000', nameDe: 'Hühnerei', nameEn: 'Hen egg' },
      grams: 55,
      via: 'eggSize',
      corrected: false,
      energyKcal: 142.9
    }
  ],
  source
});

const complete = () => ({
  ...partial(520.4),
  complete: true,
  counted: 5,
  values: values(520.4, false),
  ingredients: partial().ingredients.map((line) =>
    line.status === 'counted'
      ? line
      : {
          ...line,
          status: 'counted',
          food: { code: 'X', nameDe: 'Salz', nameEn: 'Salt' },
          grams: 1,
          via: 'mass',
          energyKcal: 0
        }
  )
});

const nothing = () => ({
  ...partial(),
  counted: 0,
  values: values(0, true),
  ingredients: partial().ingredients.map((line) => ({
    ingredientId: line.ingredientId,
    status: 'unknownFood',
    corrected: false
  }))
});

function serverAnswers(body: unknown | (() => Response)) {
  const fetched = vi.fn(() =>
    Promise.resolve(
      typeof body === 'function'
        ? body()
        : new Response(JSON.stringify(body), {
            status: 200,
            headers: { 'Content-Type': 'application/json' }
          })
    )
  );

  vi.stubGlobal('fetch', fetched);

  return fetched;
}

const show = (servings = 2) =>
  renderWithProviders(NutritionPanel, { props: { recipe, servings, householdId: 'h1' } });

const headline = () =>
  waitFor(() => {
    const summary = document.querySelector('summary');

    expect(summary).not.toBeNull();

    return summary!;
  });

beforeEach(() => preferences.setLocale('en'));

afterEach(() => {
  nutrition.reset();
  preferences.setLocale('en');
  vi.unstubAllGlobals();
});

describe('the closed line', () => {
  it('says a complete figure plainly', async () => {
    serverAnswers(complete());
    show();

    const summary = await headline();

    expect(summary).toHaveTextContent('Nutrition');
    expect(summary).toHaveTextContent('520 kcal per serving');
    expect(summary).not.toHaveTextContent('ingredients');
    expect(summary).not.toHaveTextContent('at least');
  });

  it('rounds exact values the usual way', async () => {
    serverAnswers({ ...complete(), values: values(520.6, false) });
    show();

    expect(await headline()).toHaveTextContent('521 kcal');
  });

  it('writes a lower bound as one, rounded down, with what it covers', async () => {
    serverAnswers(partial(520.9));
    show();

    const summary = await headline();

    expect(summary).toHaveTextContent('at least 520 kcal per serving · 3 of 5 ingredients');
    expect(summary).not.toHaveTextContent('≥');
  });

  it('says per piece when the recipe makes pieces', async () => {
    serverAnswers({ ...complete(), per: 'piece' });
    show();

    expect(await headline()).toHaveTextContent('520 kcal per piece');
  });

  it('offers no figure when nothing could be counted', async () => {
    serverAnswers(nothing());
    show();

    const summary = await headline();

    expect(summary).toHaveTextContent('Nothing in this recipe can be counted');
    expect(summary).not.toHaveTextContent('kcal');
  });

  it('speaks German, with decimal commas', async () => {
    preferences.setLocale('de');
    serverAnswers(partial(520.9));
    show();

    const summary = await headline();

    expect(summary).toHaveTextContent('Nährwerte');
    expect(summary).toHaveTextContent('mind. 520 kcal pro Portion · 3 von 5 Zutaten');

    await userEvent.click(summary);

    expect(
      screen.getByRole('rowheader', { name: 'davon gesättigte Fettsäuren' })
    ).toBeInTheDocument();
    expect(
      screen.getByRole('rowheader', { name: 'Kohlenhydrate' }).closest('tr')
    ).toHaveTextContent('mind. 7,4 g');
  });
});

describe('opened', () => {
  it('shows the label, row by row, with a lower bound as one', async () => {
    serverAnswers(partial(520.9));
    show();
    await userEvent.click(await headline());

    const table = screen.getByRole('table');
    const row = (name: string) =>
      within(within(table).getByRole('rowheader', { name }).closest('tr')!);

    expect(row('Energy').getByRole('cell')).toHaveTextContent('at least 2,179 kJ / 520 kcal');
    expect(row('Fat').getByRole('cell')).toHaveTextContent('at least 31 g');
    expect(row('of which saturates').getByRole('cell')).toHaveTextContent('at least 19 g');
    expect(row('Carbohydrate').getByRole('cell')).toHaveTextContent('at least 7.4 g');
    expect(row('of which sugars').getByRole('cell')).toHaveTextContent('at least 3.0 g');
    expect(row('Protein').getByRole('cell')).toHaveTextContent('at least 9.2 g');
    expect(row('Salt').getByRole('cell')).toHaveTextContent('at least 0.45 g');
    expect(within(table).getByRole('columnheader', { name: 'per serving' })).toBeInTheDocument();
  });

  it('rounds an exact label the way a package does', async () => {
    serverAnswers(complete());
    show();
    await userEvent.click(await headline());

    const cell = (name: string) =>
      screen.getByRole('rowheader', { name }).closest('tr')!.querySelector('td')!;

    expect(cell('Fat')).toHaveTextContent(/^32\s*g$/);
    expect(cell('Carbohydrate')).toHaveTextContent(/^7\.5\s*g$/);
    expect(cell('Salt')).toHaveTextContent(/^0\.46\s*g$/);
  });

  it('shows what was counted as what, and what was not and why', async () => {
    serverAnswers(partial());
    show();
    await userEvent.click(await headline());

    const counted = screen.getByRole('heading', { name: 'Counted' })
      .nextElementSibling as HTMLElement;
    const lines = within(counted).getAllByRole('listitem');

    expect(lines).toHaveLength(3);
    expect(lines[0]).toHaveTextContent('200 g butter');
    expect(lines[0]).toHaveTextContent('as Butter');
    expect(lines[0]).not.toHaveTextContent('as Butter · ');
    expect(lines[0]).toHaveTextContent('372 kcal');
    expect(lines[1]).toHaveTextContent('as Onion, raw · ≈ 27 g · by density');
    expect(lines[2]).toHaveTextContent('as Hen egg · ≈ 55 g · egg size M');

    const not = screen.getByRole('heading', { name: 'Not counted' })
      .nextElementSibling as HTMLElement;
    const left = within(not).getAllByRole('listitem');

    expect(left).toHaveLength(2);
    expect(left[0]).toHaveTextContent('salt');
    expect(left[0]).toHaveTextContent('no amount');
    expect(left[1]).toHaveTextContent('olive oil');
    expect(left[1]).toHaveTextContent('amount not in grams');
    expect(
      screen.getByText(/missing from the sum, so the real values are higher/)
    ).toBeInTheDocument();
  });

  it('says nothing about missing lines when every line is counted', async () => {
    serverAnswers(complete());
    show();
    await userEvent.click(await headline());

    expect(screen.queryByText(/missing from the sum/)).not.toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Not counted' })).not.toBeInTheDocument();
  });

  it("names the other group in the reader's language and uses the household's excluded word", async () => {
    serverAnswers({
      ...partial(),
      ingredients: partial().ingredients.map((line) =>
        line.ingredientId === salt ? { ...line, status: 'excluded' } : line
      )
    });
    show();
    await userEvent.click(await headline());

    expect(screen.getByText('excluded by your household')).toBeInTheDocument();
  });

  it('has the label out of the way when nothing could be counted, and still says why', async () => {
    serverAnswers(nothing());
    show();
    await userEvent.click(await headline());

    expect(screen.queryByRole('table')).not.toBeInTheDocument();
    expect(screen.getAllByText('not in the table')).toHaveLength(5);
  });

  it('credits the table, linking its source', async () => {
    serverAnswers(partial());
    show();
    await userEvent.click(await headline());

    const credit = screen.getAllByText(/Nutrition values:/)[0]!.closest('p')!;

    expect(credit).toHaveTextContent(
      'Nutrition values: Max Rubner-Institut, Bundeslebensmittelschlüssel 4.0 (CC BY 4.0)'
    );
    expect(
      within(credit).getByRole('link', { name: /Bundeslebensmittelschlüssel 4.0/ })
    ).toHaveAttribute('href', 'https://blsdb.de');
    expect(within(credit).getByRole('link')).toHaveAttribute(
      'rel',
      expect.stringContaining('noopener')
    );
  });

  it('prints its credit even while closed', async () => {
    serverAnswers(partial());
    show();
    await headline();

    expect(document.querySelectorAll('.paper')).toHaveLength(1);

    await userEvent.click(await headline());

    expect(document.querySelectorAll('.paper')).toHaveLength(0);
  });
});

describe('servings', () => {
  it('scales what is counted with the ingredient list, never the figures per portion', async () => {
    serverAnswers(partial(520.9));
    show(4);
    await userEvent.click(await headline());

    const counted = screen.getByRole('heading', { name: 'Counted' })
      .nextElementSibling as HTMLElement;
    const lines = within(counted).getAllByRole('listitem');

    // Twice the servings, twice the butter: amount and grams double, the energy per portion does not.
    expect(lines[0]).toHaveTextContent('400 g butter');
    expect(lines[0]).toHaveTextContent('as Butter');
    expect(lines[0]).toHaveTextContent('372 kcal');
    expect(lines[1]).toHaveTextContent('≈ 55 g · by density');
    expect(lines[1]).toHaveTextContent('6 kcal');
    expect(await headline()).toHaveTextContent('at least 520 kcal per serving');
    expect(screen.getByRole('rowheader', { name: 'Fat' }).closest('tr')).toHaveTextContent(
      'at least 31 g'
    );
  });
});

describe('before and when it fails', () => {
  it('shows a skeleton, not a spinner, once the wait is noticeable', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(() => new Promise<Response>(() => {}))
    );
    show();

    expect(document.querySelector('[aria-busy="true"]')).toBeNull();

    await waitFor(() =>
      expect(screen.getByLabelText('Loading nutrition values')).toHaveAttribute('aria-busy', 'true')
    );
  });

  it('says plainly that the values are not available, and asks again on request', async () => {
    const answers = [
      () => new Response(null, { status: 503 }),
      () =>
        new Response(JSON.stringify(complete()), {
          headers: { 'Content-Type': 'application/json' }
        })
    ];

    vi.stubGlobal(
      'fetch',
      vi.fn(() => Promise.resolve(answers.shift()!()))
    );
    show();

    expect(
      await screen.findByText('Nutrition values are not available right now.')
    ).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Try again' }));

    expect(await headline()).toHaveTextContent('520 kcal per serving');
  });

  it('asks for the household reading the recipe', async () => {
    const fetched = serverAnswers(complete());

    show();
    await headline();

    // Asked once: the effect that asks must not re-run on what asking writes.
    await new Promise((resume) => setTimeout(resume, 50));
    expect(fetched).toHaveBeenCalledTimes(1);

    const request = fetched.mock.calls[0] as unknown as [Request];

    expect(new URL(request[0].url).pathname).toBe('/api/v1/recipes/r1/nutrition');
    expect(new URL(request[0].url).searchParams.get('householdId')).toBe('h1');
  });
});
