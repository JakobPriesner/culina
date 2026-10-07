import { screen } from '@testing-library/svelte';
import { describe, expect, it } from 'vitest';

import CookbookCard from './CookbookCard.svelte';
import type { Cookbook } from './types';
import { renderWithProviders } from '$lib/test/render';

const cookbook = (over: Partial<Cookbook> = {}): Cookbook => ({
  id: 'c1',
  name: 'Christmas',
  description: null,
  kind: 'manual',
  rules: null,
  recipeCount: 3,
  cover: [],
  updatedAt: '2026-09-14T00:00:00Z',
  ...over
});

describe('a cookbook on the shelf', () => {
  it('links to the cookbook by a name that says which one', () => {
    renderWithProviders(CookbookCard, { props: { cookbook: cookbook() } });

    expect(screen.getByRole('link', { name: /Open Christmas/ })).toHaveAttribute(
      'href',
      '/cookbooks/c1'
    );
  });

  it('says how much is on it, which is the one fact a cover cannot show', () => {
    renderWithProviders(CookbookCard, { props: { cookbook: cookbook() } });

    expect(screen.getByText('3 recipes')).toBeInTheDocument();
  });

  it('draws a cover from the recipes on it', () => {
    renderWithProviders(CookbookCard, {
      props: {
        cookbook: cookbook({
          cover: [
            { recipeId: 'r1', imageId: 'i1' },
            { recipeId: 'r2', imageId: 'i2' }
          ]
        })
      }
    });

    expect(screen.getAllByRole('presentation', { hidden: true })).toHaveLength(2);

    expect(screen.queryAllByRole('presentation')).toHaveLength(0);
  });

  it('addresses each cover picture by the picture it is, so a replaced one is fetched again', () => {
    renderWithProviders(CookbookCard, {
      props: { cookbook: cookbook({ cover: [{ recipeId: 'r1', imageId: 'i1' }] }) }
    });

    const [tile] = screen.getAllByRole('presentation', { hidden: true });

    expect(tile).toHaveAttribute('src', expect.stringContaining('/recipes/r1/image?w=400&v=i1'));
  });

  it('falls back to the initial rather than a grey box when nothing is photographed', () => {
    renderWithProviders(CookbookCard, { props: { cookbook: cookbook({ name: 'Sunday' }) } });

    expect(screen.getByText('S')).toBeInTheDocument();
  });

  it('says in words that a smart cookbook fills itself', () => {
    renderWithProviders(CookbookCard, {
      props: {
        cookbook: cookbook({
          kind: 'smart',
          rules: { tags: ['hauptspeise'], ingredients: [], maxMinutes: null }
        })
      }
    });

    expect(screen.getByText('Automatic')).toBeInTheDocument();
  });

  it('says nothing of the sort about one somebody fills by hand', () => {
    renderWithProviders(CookbookCard, { props: { cookbook: cookbook() } });

    expect(screen.queryByText('Automatic')).not.toBeInTheDocument();
  });

  it('says it is busy while a change to it is in flight', () => {
    renderWithProviders(CookbookCard, { props: { cookbook: cookbook(), pending: true } });

    expect(screen.getByRole('article')).toHaveAttribute('aria-busy', 'true');
  });
});
