import { fireEvent, screen } from '@testing-library/svelte';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { pageSearch } from '$features/recipes/filters/pageSearch.svelte';
import { searchOverlay } from '$features/recipes/search/overlayState.svelte';
import { renderWithProviders } from '$lib/test/render';

import SearchButton from './SearchButton.svelte';

/* The header's search: the page's own field where it has one, the overlay everywhere else. */
afterEach(() => {
  pageSearch.reveal = null;
  searchOverlay.hide();
});

describe('the header search button', () => {
  it('opens the search over a page without a field of its own', async () => {
    renderWithProviders(SearchButton);

    await fireEvent.click(screen.getByRole('button', { name: 'Search recipes' }));

    expect(searchOverlay.open).toBe(true);
  });

  it("leads to the page's own search field instead", async () => {
    const reveal = vi.fn();
    pageSearch.reveal = reveal;
    renderWithProviders(SearchButton);

    await fireEvent.click(screen.getByRole('button', { name: 'Search recipes' }));

    expect(reveal).toHaveBeenCalledOnce();
    expect(searchOverlay.open).toBe(false);
  });
});
