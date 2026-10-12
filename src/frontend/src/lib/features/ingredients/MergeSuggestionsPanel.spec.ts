import { screen, waitFor } from '@testing-library/svelte';
import { userEvent } from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import MergeSuggestionsPanel from './MergeSuggestionsPanel.svelte';
import { ingredientNames } from './stores/ingredientNames.svelte';
import { renderWithProviders } from '$lib/test/render';
import { preferences } from '$shell/preferences.svelte';

const chicken = {
  from: { name: 'Hühnchenbrustfilet', recipeCount: 1 },
  to: { name: 'Hähnchenbrust', recipeCount: 3 },
  reason: 'same_food'
};
const oil = {
  from: { name: 'Olivenoel', recipeCount: 1 },
  to: { name: 'Olivenöl', recipeCount: 4 },
  reason: 'spelling'
};

const json = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });

let calls: { method: string; url: string; body: string }[];
let suggestions: unknown[];

/** A server that forgets a suggestion once it is merged or dismissed; `refuse` answers every change with a 400. */
function serve(refuse = false) {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: Request) => {
      const body = await input.clone().text();
      calls.push({ method: input.method, url: decodeURIComponent(input.url), body });

      if (input.method !== 'GET' && refuse) {
        return json({ code: 'ingredients.same_name', detail: 'no' }, 400);
      }

      if (input.method === 'POST') {
        const { from, to } = JSON.parse(body) as { from: string; to: string };
        suggestions = suggestions.filter((one) => (one as typeof chicken).from.name !== from);

        return json({ from, to, recipeCount: 1 }, 201);
      }

      if (input.method === 'DELETE') {
        suggestions = suggestions.slice(1);

        return new Response(null, { status: 204 });
      }

      return input.url.endsWith('/ingredient-merge-suggestions')
        ? json({ items: suggestions })
        : json({ items: [] });
    })
  );
}

async function show(onmerged = vi.fn()) {
  await ingredientNames.load('h1');
  renderWithProviders(MergeSuggestionsPanel, { props: { householdId: 'h1', onmerged } });

  return onmerged;
}

beforeEach(() => {
  calls = [];
  suggestions = [chicken, oil];
  preferences.setLocale('en');
});

afterEach(() => {
  ingredientNames.reset();
  vi.unstubAllGlobals();
});

describe('suggested merges', () => {
  it('names both spellings, why they look alike, and how often each is used', async () => {
    serve();
    await show();

    expect(screen.getByText('Hühnchenbrustfilet')).toBeInTheDocument();
    expect(screen.getByText('Hähnchenbrust')).toBeInTheDocument();
    expect(screen.getByText('Same food · 1 recipe → 3 recipes')).toBeInTheDocument();
  });

  it('merges into the more used name and tells the page', async () => {
    serve();
    const onmerged = await show();

    await userEvent.click(
      screen.getByRole('button', { name: 'Merge “Hühnchenbrustfilet” into “Hähnchenbrust”' })
    );

    await waitFor(() => expect(screen.queryByText('Hühnchenbrustfilet')).not.toBeInTheDocument());
    expect(calls).toContainEqual(
      expect.objectContaining({
        method: 'POST',
        body: JSON.stringify({ from: 'Hühnchenbrustfilet', to: 'Hähnchenbrust' })
      })
    );
    expect(onmerged).toHaveBeenCalled();
  });

  it('keeps a name apart without merging it', async () => {
    serve();
    const onmerged = await show();

    await userEvent.click(
      screen.getByRole('button', { name: 'Keep “Hühnchenbrustfilet” apart from “Hähnchenbrust”' })
    );

    await waitFor(() => expect(screen.queryByText('Hühnchenbrustfilet')).not.toBeInTheDocument());
    expect(calls).toContainEqual(
      expect.objectContaining({
        method: 'DELETE',
        url: expect.stringContaining('/ingredient-merge-suggestions/Hühnchenbrustfilet')
      })
    );
    expect(onmerged).not.toHaveBeenCalled();
  });

  it('merges them all in one go', async () => {
    serve();
    await show();

    await userEvent.click(screen.getByRole('button', { name: 'Merge all' }));

    await screen.findByText('Nothing to tidy up: every ingredient has one name.');
    expect(calls.filter((call) => call.method === 'POST')).toHaveLength(2);
  });

  it('puts the suggestion back when the server refuses', async () => {
    serve(true);
    await show();

    await userEvent.click(
      screen.getByRole('button', { name: 'Merge “Hühnchenbrustfilet” into “Hähnchenbrust”' })
    );

    await waitFor(() => expect(calls.some((call) => call.method === 'POST')).toBe(true));
    expect(await screen.findByText('Hühnchenbrustfilet')).toBeInTheDocument();
  });
});
