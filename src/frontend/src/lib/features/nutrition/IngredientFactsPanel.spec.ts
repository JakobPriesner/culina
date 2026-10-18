import { screen, waitFor, within } from '@testing-library/svelte';
import { userEvent } from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import IngredientFactsPanel from './IngredientFactsPanel.svelte';
import { ingredientFacts } from './stores/ingredientFacts.svelte';
import { renderWithProviders } from '$lib/test/render';
import { preferences } from '$shell/preferences.svelte';

const butter = {
  name: 'butter',
  corrected: true,
  food: {
    code: 'M1',
    nameDe: 'Süßrahmbutter',
    nameEn: 'Sweet cream butter',
    labelDe: 'Süßrahmbutter',
    labelEn: 'Sweet cream butter'
  },
  weights: []
};
const garlic = {
  name: 'knoblauch',
  corrected: false,
  weights: [{ unit: 'clove', grams: 4 }]
};
const salt = { name: 'salz', corrected: true, weights: [] };

const json = (body: unknown) =>
  new Response(JSON.stringify(body), { headers: { 'Content-Type': 'application/json' } });

let calls: { method: string; url: string; body: string }[];

function serve(items: unknown[], useTypicalWeights = true) {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: Request) => {
      calls.push({ method: input.method, url: input.url, body: await input.clone().text() });

      if (input.method !== 'GET') {
        return new Response(null, { status: 204 });
      }

      if (input.url.includes('/foods')) {
        return json({ items: [] });
      }

      return input.url.endsWith('/nutrition/ingredients')
        ? json({ items })
        : json({ useTypicalWeights });
    })
  );
}

const show = () => renderWithProviders(IngredientFactsPanel, { props: { householdId: 'h1' } });

beforeEach(() => {
  calls = [];
  preferences.setLocale('en');
  HTMLDialogElement.prototype.showModal = function showModal(this: HTMLDialogElement) {
    this.open = true;
  };
  HTMLDialogElement.prototype.close = function close(this: HTMLDialogElement) {
    this.open = false;
  };
});

afterEach(() => {
  ingredientFacts.reset();
  vi.unstubAllGlobals();
});

describe('the household’s ingredients', () => {
  it('has the switch for typical weights, on until turned off, with what it means', async () => {
    serve([]);
    show();

    const toggle = await screen.findByRole('switch', { name: 'Use typical weights' });

    await waitFor(() => expect(toggle).toBeEnabled());
    expect(toggle).toBeChecked();
    expect(
      screen.getByText(
        'Counts e.g. 1 onion as 110 g. Off: only what is weighed or measured counts.'
      )
    ).toBeInTheDocument();
  });

  it('shows the switch off when the household turned it off, and turns it back on', async () => {
    serve([], false);
    show();

    const toggle = await screen.findByRole('switch', { name: 'Use typical weights' });

    await waitFor(() => expect(toggle).toBeEnabled());
    expect(toggle).not.toBeChecked();

    await userEvent.click(toggle);

    await waitFor(() => expect(calls.some((call) => call.method === 'PUT')).toBe(true));
    expect(JSON.parse(calls.find((call) => call.method === 'PUT')!.body)).toEqual({
      useTypicalWeights: true
    });
    expect(toggle).toBeChecked();
  });

  it('names its placeholders on a status while it waits, which a plain element may not carry', () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(() => new Promise<Response>(() => {}))
    );
    show();

    expect(screen.getByRole('status', { name: 'Loading ingredients' })).toHaveAttribute(
      'aria-busy',
      'true'
    );
  });

  it('lists each name with its food and its weights', async () => {
    serve([butter, garlic, salt]);
    show();

    const list = await screen.findByRole('list', { name: 'Ingredients' });

    expect(within(list).getByRole('heading', { name: 'Butter' })).toBeInTheDocument();
    expect(within(list).getByText('Counts as Sweet cream butter')).toBeInTheDocument();
    expect(within(list).getByText('1 clove = 4 g')).toBeInTheDocument();
    expect(within(list).getByText('Not counted')).toBeInTheDocument();
  });

  it('says in one sentence where to set ingredients when there are none', async () => {
    serve([]);
    show();

    expect(
      await screen.findByText('You can set ingredients in the nutrition breakdown of a recipe.')
    ).toBeInTheDocument();
    expect(screen.queryByRole('list', { name: 'Ingredients' })).not.toBeInTheDocument();
  });

  it('says plainly when it cannot be read, and tries again on request', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(() => Promise.resolve(new Response(null, { status: 503 })))
    );
    show();

    expect(await screen.findByText('The ingredients could not be loaded.')).toBeInTheDocument();

    serve([garlic]);
    await userEvent.click(screen.getByRole('button', { name: 'Try again' }));

    expect(await screen.findByText('1 clove = 4 g')).toBeInTheDocument();
  });

  it('removes a weight, naming which', async () => {
    serve([garlic]);
    show();

    await userEvent.click(
      await screen.findByRole('button', { name: '“knoblauch”: remove 1 clove = 4 g' })
    );

    await waitFor(() => expect(calls.some((call) => call.method === 'DELETE')).toBe(true));
    expect(calls.find((call) => call.method === 'DELETE')?.url).toContain(
      '/households/h1/ingredients/knoblauch/units/clove'
    );
  });

  it('changes a weight in place', async () => {
    serve([garlic]);
    show();

    await userEvent.click(
      await screen.findByRole('button', { name: '“knoblauch”: change 1 clove = 4 g' })
    );

    const field = screen.getByRole('textbox', {
      name: 'How much does 1 clove of “knoblauch” weigh?'
    });

    expect(field).toHaveValue('4');

    await userEvent.clear(field);
    await userEvent.type(field, '5');
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));

    await waitFor(() => expect(calls.some((call) => call.method === 'PUT')).toBe(true));

    const put = calls.find((call) => call.method === 'PUT')!;

    expect(put.url).toContain('/households/h1/ingredients/knoblauch/units/clove');
    expect(JSON.parse(put.body)).toEqual({ grams: 5 });
  });

  it('removes a food choice, and changes it in the sheet', async () => {
    serve([butter]);
    show();

    await userEvent.click(
      await screen.findByRole('button', { name: '“butter”: remove Counts as Sweet cream butter' })
    );

    await waitFor(() => expect(calls.some((call) => call.method === 'DELETE')).toBe(true));
    expect(calls.find((call) => call.method === 'DELETE')?.url).toMatch(
      /\/households\/h1\/ingredients\/butter$/
    );
  });

  it('opens the sheet to change a food', async () => {
    serve([butter]);
    show();

    await userEvent.click(
      await screen.findByRole('button', { name: '“butter”: change Counts as Sweet cream butter' })
    );

    expect(await screen.findByRole('dialog', { name: 'What is “butter”?' })).toBeInTheDocument();
  });

  it('speaks German', async () => {
    preferences.setLocale('de');
    serve([garlic]);
    show();

    expect(
      await screen.findByRole('switch', { name: 'Typische Gewichte verwenden' })
    ).toBeVisible();
    expect(await screen.findByText('1 Zehe = 4 g')).toBeInTheDocument();
  });
});
