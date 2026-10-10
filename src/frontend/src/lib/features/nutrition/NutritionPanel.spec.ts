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
      food: {
        code: 'M110100',
        nameDe: 'Butter',
        nameEn: 'Butter',
        labelDe: 'Butter',
        labelEn: 'Butter'
      },
      grams: 200,
      via: 'mass',
      corrected: false,
      energyKcal: 372.4
    },
    {
      ingredientId: onion,
      status: 'counted',
      food: {
        code: 'G410100',
        nameDe: 'Zwiebel, roh',
        nameEn: 'Onion, raw',
        labelDe: 'Zwiebel, roh',
        labelEn: 'Onion, raw'
      },
      grams: 27.4,
      via: 'density',
      corrected: false,
      energyKcal: 5.6
    },
    { ingredientId: salt, status: 'noAmount', corrected: false, canRaiseEnergy: true },
    {
      ingredientId: oil,
      status: 'amountNotInGrams',
      reason: 'spoonOfSolid',
      corrected: false,
      canRaiseEnergy: true
    },
    {
      ingredientId: egg,
      status: 'counted',
      food: {
        code: 'E110000',
        nameDe: 'Hühnerei',
        nameEn: 'Hen egg',
        labelDe: 'Hühnerei',
        labelEn: 'Hen egg'
      },
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
          food: { code: 'X', nameDe: 'Salz', nameEn: 'Salt', labelDe: 'Salz', labelEn: 'Salt' },
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

    expect(summary).toHaveTextContent('at least 520 kcal per serving · without salt and olive oil');
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
    expect(summary).toHaveTextContent('mind. 520 kcal pro Portion · ohne salt und olive oil');

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

    const counted = screen.getByRole('heading', { name: 'Counted (3)' })
      .nextElementSibling as HTMLElement;
    const lines = within(counted).getAllByRole('listitem');

    expect(lines).toHaveLength(3);
    expect(lines[0]).toHaveTextContent('200 g butter');
    expect(lines[0]).toHaveTextContent('as Butter');
    expect(lines[0]).not.toHaveTextContent('as Butter · ');
    expect(lines[0]).toHaveTextContent('372 kcal');
    expect(lines[1]).toHaveTextContent('as Onion, raw · ≈ 27 g · by density');
    expect(lines[2]).toHaveTextContent('as Hen egg · ≈ 55 g · egg size M');

    const not = screen.getByRole('heading', { name: 'Not counted (2)' })
      .nextElementSibling as HTMLElement;
    const left = within(not).getAllByRole('listitem');

    expect(left).toHaveLength(2);
    expect(left[0]).toHaveTextContent('salt');
    expect(left[0]).toHaveTextContent('no amount');
    expect(left[1]).toHaveTextContent('olive oil');
    expect(left[1]).toHaveTextContent('spoonful of a solid – too imprecise');
    expect(
      screen.getByText(/missing from the sum, so the real values are higher/)
    ).toBeInTheDocument();
  });

  it('says nothing about missing lines when every line is counted', async () => {
    serverAnswers(complete());
    show();
    await userEvent.click(await headline());

    expect(screen.queryByText(/missing from the sum/)).not.toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Not counted (2)' })).not.toBeInTheDocument();
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
    expect(screen.getAllByText('not in the food table')).toHaveLength(5);
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

    const counted = screen.getByRole('heading', { name: 'Counted (3)' })
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

/** Which lines could still raise the energy, by ingredient. */
const flagged = (answer: ReturnType<typeof partial>, ...ids: string[]) => ({
  ...answer,
  ingredients: answer.ingredients.map((line) => ({
    ...line,
    canRaiseEnergy: ids.includes(line.ingredientId)
  }))
});

const withLine = (answer: ReturnType<typeof partial>, id: string, change: object) => ({
  ...answer,
  ingredients: answer.ingredients.map((line) =>
    line.ingredientId === id ? { ...line, ...change } : line
  )
});

describe('the headline names what is missing', () => {
  it('names one line', async () => {
    serverAnswers(flagged(partial(), salt));
    show();

    expect(await headline()).toHaveTextContent('at least 520 kcal per serving · without salt');
  });

  it('names two lines in recipe order, joined the way the language joins', async () => {
    serverAnswers(flagged(partial(), oil, onion));
    show();

    expect(await headline()).toHaveTextContent('· without onion and olive oil');
  });

  it('names two and counts the rest', async () => {
    serverAnswers(flagged(partial(), onion, salt, oil));
    show();

    expect(await headline()).toHaveTextContent('· without onion, salt and 1 more');
  });

  it('speaks German', async () => {
    preferences.setLocale('de');
    serverAnswers(flagged(partial(), onion, salt, oil, egg));
    show();

    expect(await headline()).toHaveTextContent(
      'mind. 520 kcal pro Portion · ohne onion, salt und 2 weitere'
    );
  });

  it('names nothing when the energy is exact, however many lines were left out', async () => {
    serverAnswers({ ...flagged(partial()), values: values(520.4, false) });
    show();

    const summary = await headline();

    expect(summary).toHaveTextContent('520 kcal per serving');
    expect(summary).not.toHaveTextContent('without');
    expect(summary).not.toHaveTextContent('at least');
    expect(summary).not.toHaveTextContent('ingredients');
  });
});

describe('a recipe that makes one portion', () => {
  it('says whole recipe, and offers to set the servings', async () => {
    serverAnswers({ ...complete(), yield: 1 });
    show();

    const summary = await headline();

    expect(summary).toHaveTextContent('520 kcal for the whole recipe');
    expect(summary).not.toHaveTextContent('per serving');

    await userEvent.click(summary);

    expect(screen.getByRole('columnheader', { name: 'whole recipe' })).toBeInTheDocument();
    expect(screen.getByText(/The recipe says 1 serving\./)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Set servings' })).toHaveAttribute(
      'href',
      '/recipes/r1/edit#yield'
    );
  });

  it('does not say it for pieces, or for a recipe of several servings', async () => {
    serverAnswers({ ...complete(), per: 'piece', yield: 1 });
    show();
    await userEvent.click(await headline());

    expect(screen.queryByText(/whole recipe/)).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Set servings' })).not.toBeInTheDocument();
  });
});

describe('an implausible amount', () => {
  const milk = () =>
    withLine(partial(), oil, { status: 'implausible', grams: 1800000, via: 'density' });

  it('is hinted at in the closed line, and sits with what was not counted', async () => {
    serverAnswers(milk());
    show();

    const summary = await headline();

    expect(summary).toHaveTextContent(/Is this amount right\? .*olive oil/);

    await userEvent.click(summary);

    const not = screen.getByRole('heading', { name: /Not counted/ })
      .nextElementSibling as HTMLElement;

    expect(not).toHaveTextContent('amount looks wrong, not counted');
  });

  it('is not also named among what is missing', async () => {
    serverAnswers(flagged(milk(), salt, oil));
    show();

    const summary = await headline();

    expect(summary).toHaveTextContent('· without salt · Is this amount right?');
    expect(summary).not.toHaveTextContent('olive oil ·');
    expect(summary).not.toHaveTextContent('and 1 more');
  });

  it('is a calm notice at the top of the open panel, linking to that line in the editor', async () => {
    serverAnswers(milk());
    show();
    await userEvent.click(await headline());

    const link = screen.getByRole('link', { name: 'Fix in the recipe' });

    expect(link).toHaveAttribute('href', `/recipes/r1/edit#ingredient-${oil}`);
    expect(link.closest('li')).toHaveTextContent(/olive oil looks unusual and isn't counted\./);
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('is absent when every amount is believable', async () => {
    serverAnswers(partial());
    show();
    await userEvent.click(await headline());

    expect(screen.queryByText(/Is this amount right/)).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Fix in the recipe' })).not.toBeInTheDocument();
  });
});

describe('why a line is not counted', () => {
  it.each([
    [
      'spoonOfSolid',
      'spoonful of a solid – too imprecise',
      'Löffel einer festen Zutat – zu ungenau'
    ],
    [
      'volumeOfSolid',
      'volume of a solid – too imprecise',
      'Volumen einer festen Zutat – zu ungenau'
    ],
    ['count', 'a count, not a weight', 'Stückzahl, kein Gewicht'],
    ['householdUnit', 'own unit, not a weight', 'eigene Einheit, kein Gewicht']
  ])('says %s in both languages', async (reason, english, german) => {
    for (const [locale, words] of [
      ['en', english],
      ['de', german]
    ] as const) {
      preferences.setLocale(locale);
      serverAnswers(withLine(partial(), oil, { reason }));

      const view = show();

      await userEvent.click(await headline());
      expect(screen.getByText(words)).toBeInTheDocument();

      view.unmount();
      nutrition.reset();
    }
  });

  it('says the other reasons too', async () => {
    serverAnswers({
      ...partial(),
      ingredients: [
        { ingredientId: butter, status: 'unknownFood', corrected: false, canRaiseEnergy: true },
        { ingredientId: onion, status: 'excluded', corrected: true, canRaiseEnergy: false },
        { ingredientId: salt, status: 'noAmount', corrected: false, canRaiseEnergy: true },
        {
          ingredientId: oil,
          status: 'counted',
          food: { code: 'F', nameDe: 'Öl', nameEn: 'Oil', labelDe: 'Öl', labelEn: 'Oil' },
          grams: 10,
          via: 'mass',
          corrected: false,
          canRaiseEnergy: false,
          energyKcal: 90
        }
      ]
    });
    show();
    await userEvent.click(await headline());

    expect(screen.getByText('not in the food table')).toBeInTheDocument();
    expect(screen.getByText('excluded by your household')).toBeInTheDocument();
    expect(screen.getByText('no amount')).toBeInTheDocument();
  });
});

describe('a bound that says nothing', () => {
  it('is a dash for nothing known, read as "not known"', async () => {
    serverAnswers({
      ...partial(),
      values: {
        ...values(520.9, true),
        fat: value(0, true),
        salt: value(0, true),
        sugars: value(0.2, true)
      }
    });
    show();
    await userEvent.click(await headline());

    const cell = (name: string) =>
      screen.getByRole('rowheader', { name }).closest('tr')!.querySelector('td')!;

    expect(cell('Fat')).toHaveTextContent(/^–\s*not known$/);
    expect(cell('Salt')).toHaveTextContent(/^–\s*not known$/);
    expect(cell('Fat').querySelector('[aria-hidden="true"]')).toHaveTextContent('–');
    expect(cell('Protein')).toHaveTextContent('at least 9.2 g');
  });

  it('is still a zero when the value is exact', async () => {
    serverAnswers({ ...complete(), values: { ...values(520.4, false), fat: value(0, false) } });
    show();
    await userEvent.click(await headline());

    expect(screen.getByRole('rowheader', { name: 'Fat' }).closest('tr')).toHaveTextContent(/0\s*g/);
    expect(screen.queryByText('not known')).not.toBeInTheDocument();
  });
});

describe('friendly names', () => {
  const mince = {
    code: 'F1',
    nameDe: 'Rind/Schwein, Hackfleisch gemischt, roh',
    nameEn: 'Beef/pork, mince mixed, raw',
    labelDe: 'gemischtes Hackfleisch',
    labelEn: 'mixed mince'
  };

  const minced = () =>
    withLine(partial(), butter, { food: mince, grams: 200, via: 'mass', energyKcal: 372 });

  it('counts as the label, with the table name quietly under it', async () => {
    serverAnswers(minced());
    show();
    await userEvent.click(await headline());

    const row = screen.getAllByRole('listitem')[0]!;

    expect(row).toHaveTextContent('as mixed mince');
    expect(row).toHaveTextContent('BLS: Beef/pork, mince mixed, raw');
    expect(within(row).getByText(/BLS:/)).toBeVisible();
  });

  it('follows the reader’s language', async () => {
    preferences.setLocale('de');
    serverAnswers(minced());
    show();
    await userEvent.click(await headline());

    const row = screen.getAllByRole('listitem')[0]!;

    expect(row).toHaveTextContent('als gemischtes Hackfleisch');
    expect(row).toHaveTextContent('BLS: Rind/Schwein, Hackfleisch gemischt, roh');
  });

  it('does not repeat the table name when the label is the same words', async () => {
    serverAnswers(partial());
    show();
    await userEvent.click(await headline());

    expect(screen.queryByText(/BLS:/)).not.toBeInTheDocument();
  });
});

describe('a shared recipe, read by a visitor', () => {
  const shared = { ...recipe, id: 'a-share-token' };
  const showShared = () =>
    renderWithProviders(NutritionPanel, {
      props: { recipe: shared, servings: 2, householdId: null, readonly: true }
    });

  it('asks the share link for the figures, and credits the table', async () => {
    const fetched = serverAnswers(partial());

    showShared();

    const summary = await headline();

    expect(summary).toHaveTextContent('at least 520 kcal per serving');

    const asked = (fetched.mock.calls as unknown as [Request][])[0]![0];

    expect(new URL(asked.url).pathname).toBe('/api/v1/shared-recipes/a-share-token/nutrition');
    expect(screen.getAllByText(/Max Rubner-Institut/).length).toBeGreaterThan(0);
  });

  it('offers no correction and no way into an editor', async () => {
    serverAnswers(
      withLine({ ...partial(), yield: 1 }, oil, {
        status: 'implausible',
        grams: 1800000,
        via: 'density'
      })
    );
    showShared();

    const summary = await headline();

    // Still asks whether the amount is right, but only as a question: there is nothing to follow.
    expect(summary).toHaveTextContent(/Is this amount right\? .*olive oil/);

    await userEvent.click(summary);

    expect(screen.getByText(/The recipe says 1 serving\./)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^What is / })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Fix in the recipe' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Set servings' })).not.toBeInTheDocument();
  });
});
