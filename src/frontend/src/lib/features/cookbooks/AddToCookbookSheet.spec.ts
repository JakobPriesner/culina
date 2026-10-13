import { screen, waitFor } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { renderWithProviders } from '$lib/test/render';

import AddToCookbookSheet from './AddToCookbookSheet.svelte';
import { cookbooks } from './stores/cookbooks.svelte';

const shelf = (n: number) => ({
  cookbookId: `c${n}`,
  name: `Cookbook ${n}`,
  description: null,
  recipeCount: 0,
  coverRecipeIds: [],
  coverPictures: [],
  updatedAt: '2026-09-14T00:00:00Z'
});

const json = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' }
  });

/** Thirty cookbooks in pages of 24, as the server pages them. */
const shelves = (url: URL) =>
  url.searchParams.get('cursor')
    ? json({ items: Array.from({ length: 6 }, (_, i) => shelf(25 + i)), nextCursor: null })
    : json({ items: Array.from({ length: 24 }, (_, i) => shelf(1 + i)), nextCursor: 'page-2' });

function serverAnswers(list: (url: URL) => Response) {
  vi.stubGlobal(
    'fetch',
    vi.fn((input: Request) => {
      const url = new URL(input.url);

      return Promise.resolve(
        url.pathname === '/api/v1/cookbooks' ? list(url) : json({ items: [] })
      );
    })
  );
}

const render = () =>
  renderWithProviders(AddToCookbookSheet, {
    props: { open: true, householdId: 'h1', recipeId: 'r1', onclose: () => {} }
  });

/* jsdom has <dialog> but not the top layer, so showModal is the open state. */
beforeEach(() => {
  HTMLDialogElement.prototype.showModal = function showModal(this: HTMLDialogElement) {
    this.open = true;
  };
  cookbooks.reset();
});

describe('adding a recipe to a cookbook', () => {
  it('offers every cookbook, not only the first page of them', async () => {
    serverAnswers(shelves);
    render();

    await waitFor(() => expect(screen.getAllByRole('checkbox')).toHaveLength(30));
  });

  it('says when the cookbooks could not be read, and reads them again on request', async () => {
    serverAnswers(() => json({ code: 'server.error' }, 500));
    render();

    const retry = await screen.findByRole('button', { name: 'Try again' });

    expect(screen.getByRole('alert')).toHaveTextContent('Could not load your cookbooks');

    serverAnswers(shelves);
    await userEvent.click(retry);

    await waitFor(() => expect(screen.getAllByRole('checkbox')).toHaveLength(30));
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });
});
