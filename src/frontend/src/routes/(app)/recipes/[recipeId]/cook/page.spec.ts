import { screen, waitFor } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import CookPage from './+page.svelte';
import { clientError, ErrorCodes } from '$api';
import { session } from '$features/auth/session.svelte';
import { cooking } from '$features/cooking/stores/cooking.svelte';
import { kitchenTimers } from '$features/cooking/kitchen.svelte';
import { recipes } from '$features/recipes/stores/recipes.svelte';
import type { Recipe } from '$features/recipes/types';
import { renderWithProviders } from '$lib/test/render';

vi.mock('$app/state', () => ({
  page: { params: { recipeId: 'recipe-1' }, url: new URL('http://localhost/recipes/recipe-1/cook') }
}));

const step = (id: string) => ({
  id,
  title: null,
  segments: [],
  uses: [],
  durationSeconds: null
});

const recipe: Recipe = {
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
  groups: [],
  steps: [step('s-1'), step('s-2')],
  tags: [],
  sourceUrl: null,
  createdBy: 'user-1',
  createdAt: '2026-09-20T12:00:00Z',
  updatedAt: '2026-09-20T12:00:00Z',
  version: 1
};

beforeEach(() => {
  vi.stubGlobal(
    'IntersectionObserver',
    class {
      observe() {}
      disconnect() {}
    }
  );
  vi.spyOn(session, 'activeHouseholdId', 'get').mockReturnValue('household-1');
  vi.spyOn(recipes, 'detail', 'get').mockReturnValue(recipe);
  vi.spyOn(recipes, 'detailStatus', 'get').mockReturnValue('ready');
  vi.spyOn(recipes, 'load').mockResolvedValue();
  vi.spyOn(cooking, 'resolved', 'get').mockReturnValue(true);
  vi.spyOn(kitchenTimers, 'load').mockResolvedValue();
});

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

describe('a cook session that could not be started', () => {
  it('says so and starts again on retry', async () => {
    const start = vi
      .spyOn(cooking, 'start')
      .mockResolvedValueOnce(clientError(ErrorCodes.offline, 'Offline.'))
      .mockResolvedValue(null);

    renderWithProviders(CookPage);

    const alert = await screen.findByRole('alert');

    expect(alert.textContent).toContain('could not be started');

    await userEvent.click(screen.getByRole('button', { name: 'Try again' }));

    await waitFor(() => expect(screen.queryByRole('alert')).toBeNull());
    expect(start).toHaveBeenCalledTimes(2);
  });

  it('shows nothing when the session starts', async () => {
    vi.spyOn(cooking, 'start').mockResolvedValue(null);

    renderWithProviders(CookPage);

    await waitFor(() => expect(cooking.start).toHaveBeenCalled());
    expect(screen.queryByRole('alert')).toBeNull();
  });
});
