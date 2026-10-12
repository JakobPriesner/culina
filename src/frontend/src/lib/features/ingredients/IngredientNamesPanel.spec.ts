import { screen, waitFor } from '@testing-library/svelte';
import { userEvent } from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import IngredientNamesPanel from './IngredientNamesPanel.svelte';
import { ingredientNames } from './stores/ingredientNames.svelte';
import { renderWithProviders } from '$lib/test/render';
import { preferences } from '$shell/preferences.svelte';

const json = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });

let posted: unknown[];

function serve(names: { name: string; recipeCount: number }[]) {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: Request) => {
      if (input.method === 'POST') {
        const body = (await input.json()) as { from: string; to: string };
        posted.push(body);

        return json({ ...body, recipeCount: 1 }, 201);
      }

      return input.url.endsWith('/ingredient-names') ? json({ items: names }) : json({ items: [] });
    })
  );
}

async function show() {
  await ingredientNames.load('h1');
  renderWithProviders(IngredientNamesPanel, { props: { householdId: 'h1' } });
}

beforeEach(() => {
  posted = [];
  preferences.setLocale('en');
});

afterEach(() => {
  ingredientNames.reset();
  vi.unstubAllGlobals();
});

describe('the household’s ingredient names', () => {
  it('lists every name with how many recipes use it, and filters them', async () => {
    serve([
      { name: 'Salz', recipeCount: 4 },
      { name: 'Mehl', recipeCount: 1 }
    ]);
    await show();

    expect(screen.getByText('4 recipes')).toBeInTheDocument();
    expect(screen.getByText('1 recipe')).toBeInTheDocument();

    await userEvent.type(screen.getByRole('searchbox', { name: 'Filter ingredients' }), 'meh');

    expect(screen.queryByText('Salz')).not.toBeInTheDocument();
    expect(screen.getByText('Mehl')).toBeInTheDocument();
  });

  it('renames a name to one nobody uses yet', async () => {
    serve([{ name: 'Hühnchenbrustfilet', recipeCount: 2 }]);
    await show();

    await userEvent.click(
      screen.getByRole('button', { name: 'Rename or merge “Hühnchenbrustfilet”' })
    );

    const field = screen.getByRole('textbox', { name: 'New name for “Hühnchenbrustfilet”' });

    await userEvent.clear(field);
    await userEvent.type(field, 'Hähnchenbrust');
    await userEvent.click(screen.getByRole('button', { name: 'Rename' }));

    await waitFor(() =>
      expect(posted).toEqual([{ from: 'Hühnchenbrustfilet', to: 'Hähnchenbrust' }])
    );
  });

  it('says it merges when the new name is one already in use', async () => {
    serve([
      { name: 'Hähnchenbrust', recipeCount: 3 },
      { name: 'Hühnchenbrustfilet', recipeCount: 1 }
    ]);
    await show();

    await userEvent.click(
      screen.getByRole('button', { name: 'Rename or merge “Hühnchenbrustfilet”' })
    );

    const field = screen.getByRole('textbox', { name: 'New name for “Hühnchenbrustfilet”' });

    await userEvent.clear(field);
    await userEvent.type(field, 'hähnchenbrust');

    await userEvent.click(screen.getByRole('button', { name: 'Merge into “Hähnchenbrust”' }));

    await waitFor(() =>
      expect(posted).toEqual([{ from: 'Hühnchenbrustfilet', to: 'Hähnchenbrust' }])
    );
  });
});
