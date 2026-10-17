import { screen, waitFor } from '@testing-library/svelte';
import { userEvent } from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { session as account } from '$features/auth/session.svelte';
import NowCookingBar from './NowCookingBar.svelte';
import { cooking } from './stores/cooking.svelte';
import { renderWithProviders } from '$lib/test/render';

const page = vi.hoisted(() => ({ url: new URL('http://culina.test/') }));

vi.mock('$app/state', () => ({ page }));

/*
 * The bar leads back to the hob, and it also has to be possible to say the
 * cooking is over from wherever you happen to be standing.
 */
const session = {
  sessionId: 's1',
  recipeId: 'r1',
  recipeTitle: 'Lemon orzo',
  householdId: 'h1',
  servings: 4,
  currentStepIndex: 0,
  startedAt: '2026-09-12T12:00:00Z',
  lastActiveAt: '2026-09-12T12:00:00Z',
  version: 1
};

afterEach(() => {
  page.url = new URL('http://culina.test/');
  cooking.reset();
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

function serverAnswers(answer: object) {
  vi.stubGlobal(
    'fetch',
    vi.fn(() => Promise.resolve(new Response(JSON.stringify(answer))))
  );
}

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

  it('stays away for a session only opened and left: first step, no timer, just started', async () => {
    serverAnswers({ ...session, startedAt: new Date().toISOString() });

    await cooking.start('r1', 4);

    renderWithProviders(NowCookingBar);

    expect(screen.queryByRole('link')).toBeNull();
  });

  it('comes back once a step has been taken, however recently it started', async () => {
    serverAnswers({ ...session, startedAt: new Date().toISOString(), currentStepIndex: 1 });

    await cooking.start('r1', 4);

    renderWithProviders(NowCookingBar);

    expect(screen.getByRole('link')).toBeInTheDocument();
  });

  it('names the household when another one is on screen, and switches to it on the way back', async () => {
    vi.spyOn(account, 'activeHouseholdId', 'get').mockReturnValue('h2');
    vi.spyOn(account, 'households', 'get').mockReturnValue([
      { householdId: 'h1', name: 'Testküche' },
      { householdId: 'h2', name: 'Leere Küche' }
    ] as never);
    const select = vi.spyOn(account, 'selectHousehold').mockImplementation(() => {});
    serverAnswers(session);

    await cooking.start('r1', 4);

    renderWithProviders(NowCookingBar);

    const link = screen.getByRole('link');

    expect(link).toHaveTextContent('in Testküche');

    link.addEventListener('click', (event) => event.preventDefault());
    await userEvent.click(link);

    expect(select).toHaveBeenCalledWith('h1');
  });

  it('leaves the recipe’s own page to its cooking action, which says the same thing', async () => {
    serverAnswers(session);
    page.url = new URL('http://culina.test/recipes/r1');

    await cooking.start('r1', 4);

    renderWithProviders(NowCookingBar);

    expect(screen.queryByRole('link')).toBeNull();
  });
});
