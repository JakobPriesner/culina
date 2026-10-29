import { screen } from '@testing-library/svelte';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { session } from '$features/auth/session.svelte';
import { renderWithProviders } from '#lib/test/render.js';
import RecipeTags from './RecipeTags.svelte';

const show = (tags: string[]) =>
  renderWithProviders(RecipeTags, { props: { recipeId: 'r1', tags } });

afterEach(() => vi.restoreAllMocks());

describe('the tags on a recipe', () => {
  it('lead to the library filtered by each one', () => {
    vi.spyOn(session, 'status', 'get').mockReturnValue('authenticated');

    show(['vegetarisch', 'schnell']);

    expect(screen.getByRole('link', { name: 'vegetarisch' })).toHaveAttribute(
      'href',
      '/?tag=vegetarisch'
    );
    expect(screen.getByRole('link', { name: 'schnell' })).toBeInTheDocument();
  });

  it('are words, not links, for a visitor with no library to open', () => {
    vi.spyOn(session, 'status', 'get').mockReturnValue('anonymous');

    show(['vegetarisch']);

    expect(screen.getByText('vegetarisch')).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'vegetarisch' })).toBeNull();
  });

  it('light up a tag that arrives while the recipe is open, like an answered diet question', async () => {
    vi.spyOn(session, 'status', 'get').mockReturnValue('authenticated');

    const { rerender } = show(['schnell']);

    expect(screen.getByRole('link', { name: 'schnell' })).not.toHaveClass('fresh');

    await rerender({ recipeId: 'r1', tags: ['schnell', 'vegetarisch'] });

    expect(screen.getByRole('link', { name: 'vegetarisch' })).toHaveClass('fresh');
    expect(screen.getByRole('link', { name: 'schnell' })).not.toHaveClass('fresh');
  });

  it('take no room without any', () => {
    show([]);

    expect(screen.queryByRole('list')).toBeNull();
  });
});
