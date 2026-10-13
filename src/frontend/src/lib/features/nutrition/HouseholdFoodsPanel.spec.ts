import { screen } from '@testing-library/svelte';
import { userEvent } from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import HouseholdFoodsPanel from './HouseholdFoodsPanel.svelte';
import { householdFoods } from './stores/householdFoods.svelte';
import { renderWithProviders } from '$lib/test/render';
import { preferences } from '$shell/preferences.svelte';

const food = (id: string, name: string, own: boolean) => ({
  id,
  code: `h:${id}`,
  name,
  energyKj: 314,
  energyKcal: 75,
  own
});

let deleted: string[];

function serve(items: unknown[]) {
  deleted = [];
  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: Request) => {
      if (input.method === 'DELETE') {
        deleted.push(input.url);

        return new Response(null, { status: 204 });
      }

      return new Response(JSON.stringify({ items }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' }
      });
    })
  );
}

async function show() {
  await householdFoods.load('h1');
  renderWithProviders(HouseholdFoodsPanel, { props: { householdId: 'h1' } });
}

beforeEach(() => preferences.setLocale('en'));

afterEach(() => {
  householdFoods.reset();
  vi.unstubAllGlobals();
});

describe('the household’s foods from the package', () => {
  it('lists them with their energy, an inherited one marked and not removable here', async () => {
    serve([food('f1', 'Kokosmilch light', true), food('f2', 'Skyr light', false)]);
    await show();

    expect(screen.getByText('Kokosmilch light')).toBeInTheDocument();
    expect(screen.getAllByRole('listitem')[1]).toHaveTextContent(
      'Skyr light 75 kcal per 100 g · inherited'
    );
    expect(screen.queryByRole('button', { name: 'Remove “Skyr light”' })).not.toBeInTheDocument();
  });

  it('removes one', async () => {
    serve([food('f1', 'Kokosmilch light', true)]);
    await show();

    await userEvent.click(screen.getByRole('button', { name: 'Remove “Kokosmilch light”' }));

    expect(deleted).toEqual([expect.stringMatching(/\/households\/h1\/foods\/f1$/)]);
    expect(screen.getByText('None yet.')).toBeInTheDocument();
  });
});
