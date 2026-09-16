import { screen, waitFor } from '@testing-library/svelte';
import { userEvent } from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';

import NowCookingBar from './NowCookingBar.svelte';
import { cooking } from './stores/cooking.svelte';
import { renderWithProviders } from '$lib/test/render';

/*
 * The bar leads back to the hob, and it also has to be possible to say the
 * cooking is over from wherever you happen to be standing.
 */
const session = {
  sessionId: 's1',
  recipeId: 'r1',
  recipeTitle: 'Lemon orzo',
  servings: 4,
  currentStepIndex: 0,
  startedAt: '2026-09-12T12:00:00Z',
  lastActiveAt: '2026-09-12T12:00:00Z',
  version: 1
};

afterEach(() => {
  cooking.reset();
  vi.unstubAllGlobals();
});

describe('the bar for what is on the hob', () => {
  it('leads back to the cooking screen at the amount being cooked', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(() => Promise.resolve(new Response(JSON.stringify(session))))
    );

    await cooking.start('r1', 4);

    renderWithProviders(NowCookingBar);

    expect(screen.getByRole('link')).toHaveAttribute('href', '/recipes/r1/cook?yield=4');
  });

  it('goes away when the cook says the cooking is over', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(() => Promise.resolve(new Response(JSON.stringify(session))))
    );

    await cooking.start('r1', 4);

    renderWithProviders(NowCookingBar);

    await userEvent.click(screen.getByRole('button', { name: /stop cooking/i }));

    await waitFor(() => expect(screen.queryByRole('link')).toBeNull());
    expect(cooking.session).toBeNull();
  });
});
