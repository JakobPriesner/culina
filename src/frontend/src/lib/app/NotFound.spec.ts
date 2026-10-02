import { screen } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { session } from '$features/auth/session.svelte';
import { resetAllStores } from '$shell/stores';
import { renderWithProviders } from '$lib/test/render';

import NotFound from './NotFound.svelte';

vi.mock('$app/state', () => ({
  page: { url: new URL('http://culina.test/recipe/miso-rice') }
}));

function answerMe(status: number) {
  vi.stubGlobal(
    'fetch',
    vi.fn(() =>
      Promise.resolve(
        new Response(
          status === 200
            ? JSON.stringify({
                userId: 'u1',
                email: 'ada@example.com',
                displayName: 'Ada',
                isAdmin: false,
                createdAt: '2026-01-01T00:00:00Z',
                version: 1,
                assistance: { improve: false, draft: false, read: false, draw: false },
                households: [{ householdId: 'h1', name: 'Home', role: 'owner', inheritsFrom: [] }]
              })
            : JSON.stringify({ status }),
          { status, headers: { 'Content-Type': 'application/json' } }
        )
      )
    )
  );

  return session.refresh();
}

beforeEach(() => {
  resetAllStores();
});

describe('a page that is not there', () => {
  it('is a recipe whose first ingredient is the address, so a typo can be seen', async () => {
    await answerMe(200);

    renderWithProviders(NotFound, { props: { level: 1 } });

    expect(screen.getByRole('heading', { level: 1, name: 'Lost Page Soup' })).toBeInTheDocument();
    expect(screen.getByText(/There's no page at this address/)).toBeInTheDocument();
    expect(screen.getByText('/recipe/miso-rice')).toBeInTheDocument();
  });

  it('scales like any recipe, at 404 kcal a head', async () => {
    await answerMe(200);

    renderWithProviders(NotFound);

    await userEvent.click(screen.getByRole('button', { name: 'One more' }));

    expect(screen.getByText('Serves 2 lost cooks')).toBeInTheDocument();
    expect(screen.getByText('808 kcal')).toBeInTheDocument();
    expect(screen.getByText('2 pinches')).toBeInTheDocument();
  });

  it('lights the ingredient a step mentions', async () => {
    await answerMe(200);

    renderWithProviders(NotFound);

    await userEvent.hover(screen.getByText('Check the address for a typo.'));

    expect(screen.getByText(/address, slightly off/).closest('li')).toHaveClass('lit');
  });

  it('offers the recipes, search and the way back to something deleted', async () => {
    await answerMe(200);

    renderWithProviders(NotFound, { props: { kind: 'recipe' } });

    expect(screen.getByText('Vanished Recipe Stew')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Go to your recipes' })).toHaveAttribute('href', '/');
    expect(screen.getByRole('button', { name: 'Search your recipes' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Recently deleted' })).toHaveAttribute(
      'href',
      '/me/household'
    );
  });

  it('offers a stranger nothing but signing in', async () => {
    await answerMe(401);

    renderWithProviders(NotFound, { props: { kind: 'recipe' } });

    expect(screen.getByRole('link', { name: 'Sign in' })).toHaveAttribute('href', '/login');
    expect(screen.queryByRole('link', { name: 'Recently deleted' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Search your recipes' })).not.toBeInTheDocument();
  });
});
