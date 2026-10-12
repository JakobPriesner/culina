import { screen, waitFor } from '@testing-library/svelte';
import { userEvent } from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import IngredientNounsPanel from './IngredientNounsPanel.svelte';
import { nouns } from '$features/recipes/stores/nouns.svelte';
import { renderWithProviders } from '$lib/test/render';
import { preferences } from '$shell/preferences.svelte';

const json = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });

let sent: { method: string; url: string; body: unknown }[];

function serve(listed: { one: string; many: string }[], refuse = false) {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: Request) => {
      if (input.method === 'GET') {
        return json({ items: listed });
      }

      sent.push({
        method: input.method,
        url: decodeURIComponent(input.url),
        body: input.method === 'PUT' ? await input.json() : null
      });

      return refuse
        ? json({ code: 'ingredients.invalid_noun', detail: 'no' }, 400)
        : new Response(null, { status: 204 });
    })
  );
}

async function show() {
  await nouns.load('h1');
  renderWithProviders(IngredientNounsPanel, { props: { householdId: 'h1' } });
}

beforeEach(() => {
  sent = [];
  preferences.setLocale('en');
});

afterEach(() => {
  nouns.reset();
  vi.unstubAllGlobals();
});

describe('the household’s own words in both numbers', () => {
  it('lists them', async () => {
    serve([{ one: 'Maultasche', many: 'Maultaschen' }]);
    await show();

    expect(screen.getByText('1 Maultasche · 2 Maultaschen')).toBeInTheDocument();
  });

  it('saves a word in both numbers', async () => {
    serve([]);
    await show();

    await userEvent.type(screen.getByLabelText('Singular'), 'Schupfnudel');
    await userEvent.type(screen.getByLabelText('Plural'), 'Schupfnudeln');
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));

    expect(sent).toEqual([
      {
        method: 'PUT',
        url: expect.stringMatching(/\/households\/h1\/ingredient-nouns\/Schupfnudel$/),
        body: { many: 'Schupfnudeln' }
      }
    ]);
    expect(screen.getByText('1 Schupfnudel · 2 Schupfnudeln')).toBeInTheDocument();
    expect(screen.getByLabelText('Singular')).toHaveValue('');
  });

  it('puts the list back when the server refuses', async () => {
    serve([], true);
    await show();

    await userEvent.type(screen.getByLabelText('Singular'), 'Knödel');
    await userEvent.type(screen.getByLabelText('Plural'), 'Knödel');
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));

    await waitFor(() => expect(screen.getByText('No words of your own yet.')).toBeInTheDocument());
    expect(screen.getByLabelText('Singular')).toHaveValue('Knödel');
  });

  it('forgets one', async () => {
    serve([{ one: 'Maultasche', many: 'Maultaschen' }]);
    await show();

    await userEvent.click(
      screen.getByRole('button', { name: 'Forget the plural of “Maultasche”' })
    );

    expect(sent).toEqual([
      {
        method: 'DELETE',
        url: expect.stringMatching(/\/ingredient-nouns\/Maultasche$/),
        body: null
      }
    ]);
    expect(screen.getByText('No words of your own yet.')).toBeInTheDocument();
  });
});
