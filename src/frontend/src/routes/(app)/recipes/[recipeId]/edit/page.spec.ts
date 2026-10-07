import { screen, waitFor } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import EditPage from './+page.svelte';
import { session } from '$features/auth/session.svelte';
import { tags } from '$features/cookbooks/stores/tags.svelte';
import { recipes } from '$features/recipes/stores/recipes.svelte';
import { tagSuggestions } from '$features/recipes/stores/tagSuggestions.svelte';
import { units } from '$features/recipes/stores/units.svelte';
import type { Recipe } from '$features/recipes/types';
import { renderWithProviders } from '$lib/test/render';

/* Tested at the page, not the mention parser: a name added from inside a step has no id when mentioned, and only the following save gives it one. */
vi.mock('$app/state', () => ({ page: { params: { recipeId: 'recipe-1' } } }));

const butter = { id: 'i-butter', name: 'butter', note: null, quantity: { value: 200, unit: 'g' } };

const opened: Recipe = {
  id: 'recipe-1',
  householdId: 'household-1',
  title: 'Risotto',
  description: null,
  language: 'en',
  yieldAmount: 4,
  yieldKind: 'servings',
  yieldLabel: null,
  prepMinutes: null,
  cookMinutes: null,
  totalMinutes: null,
  imageId: null,
  groups: [{ id: 'g-1', name: null, ingredients: [butter] }],
  steps: [{ id: 's-1', title: null, segments: [], uses: [], durationSeconds: null }],
  tags: [],
  sourceUrl: null,
  createdBy: 'user-1',
  createdAt: '2026-09-20T12:00:00Z',
  updatedAt: '2026-09-20T12:00:00Z',
  version: 1
};

/** What the server keeps: the recipe as sent, with an id on every new line. */
function serverKeeps() {
  let stored = opened;
  const sent: Recipe[] = [];

  vi.spyOn(recipes, 'detail', 'get').mockImplementation(() => stored);
  vi.spyOn(recipes, 'detailStatus', 'get').mockReturnValue('ready');
  vi.spyOn(recipes, 'load').mockResolvedValue();
  vi.spyOn(recipes, 'update').mockImplementation((next) => {
    const copy = JSON.parse(JSON.stringify(next)) as Recipe;

    sent.push(copy);
    stored = {
      ...copy,
      version: copy.version + 1,
      groups: copy.groups.map((group) => ({
        ...group,
        ingredients: group.ingredients.map((one) => ({ ...one, id: one.id || `i-${one.name}` }))
      }))
    };

    return Promise.resolve(null);
  });

  return sent;
}

beforeEach(() => {
  // The rail only watches which section is on screen, which nothing here needs.
  vi.stubGlobal(
    'IntersectionObserver',
    class {
      observe() {}
      disconnect() {}
    }
  );
  vi.spyOn(session, 'activeHouseholdId', 'get').mockReturnValue('household-1');
  vi.spyOn(units, 'load').mockResolvedValue();
  vi.spyOn(tags, 'load').mockResolvedValue();
  vi.spyOn(tagSuggestions, 'load').mockResolvedValue();
});

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

describe('adding an ingredient from inside a step', () => {
  it('links the mention once the save has given the ingredient an id', async () => {
    const sent = serverKeeps();

    renderWithProviders(EditPage);

    await userEvent.type(await screen.findByRole('combobox', { name: 'Step 1' }), 'Add @saffron');
    await userEvent.click(screen.getByRole('option', { name: 'Add “saffron” to the ingredients' }));

    // Nobody types again: the save that gave saffron its id must be followed by one saying the step means it.
    await waitFor(() => expect(sent).toHaveLength(2), { timeout: 3000 });

    expect(sent[1]!.steps[0]!.segments).toContainEqual(
      expect.objectContaining({ kind: 'ingredient', ingredientId: 'i-saffron' })
    );
  });
});
