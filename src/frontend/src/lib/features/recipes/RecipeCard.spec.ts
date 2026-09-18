import { screen } from '@testing-library/svelte';
import { describe, expect, it } from 'vitest';

import RecipeCard from './RecipeCard.svelte';
import type { RecipeSummary } from './types';
import { renderWithProviders } from '$lib/test/render';

const recipe = (over: Partial<RecipeSummary> = {}): RecipeSummary => ({
  id: 'r1',
  title: 'Lemon orzo',
  imageId: null,
  totalMinutes: 25,
  yieldAmount: 4,
  yieldKind: 'servings',
  yieldLabel: null,
  tags: ['Weeknight'],
  cookCount: 0,
  lastCookedAt: null,
  updatedAt: '2026-09-12T00:00:00Z',
  match: null,
  ...over
});

const open = () => screen.getByRole('link', { name: /Open Lemon orzo/ });

describe('a recipe in the list', () => {
  it('links to the recipe by a name that says which one', () => {
    renderWithProviders(RecipeCard, { props: { recipe: recipe() } });

    expect(open()).toHaveAttribute('href', '/recipes/r1');
  });

  it('leads with the time, because that is what decides tonight', () => {
    renderWithProviders(RecipeCard, { props: { recipe: recipe() } });

    expect(screen.getByText(/^25 min/)).toBeInTheDocument();
  });

  it('says nothing about a time the recipe does not give', () => {
    renderWithProviders(RecipeCard, { props: { recipe: recipe({ totalMinutes: null }) } });

    expect(screen.getByText('4 servings')).toBeInTheDocument();
  });

  it('mentions how often it has been made, only once it has been', () => {
    renderWithProviders(RecipeCard, { props: { recipe: recipe({ cookCount: 3 }) } });

    expect(screen.getByText(/Made 3×/)).toBeInTheDocument();
  });

  it('leaves that out at zero, which is every recipe nobody has got to yet', () => {
    renderWithProviders(RecipeCard, { props: { recipe: recipe() } });

    expect(screen.queryByText(/Made/)).not.toBeInTheDocument();
  });

  it('says nothing about matching unless a match was asked for', () => {
    renderWithProviders(RecipeCard, { props: { recipe: recipe() } });

    expect(screen.queryByText(/more needed|have everything/)).not.toBeInTheDocument();
  });

  it('says what is still missing when it was', () => {
    renderWithProviders(RecipeCard, {
      props: { recipe: recipe({ match: { matched: 2, requested: 3, missing: 4 } }) }
    });

    expect(screen.getByText('4 more needed')).toBeInTheDocument();
  });

  it('says so when nothing is', () => {
    renderWithProviders(RecipeCard, {
      props: { recipe: recipe({ match: { matched: 3, requested: 3, missing: 0 } }) }
    });

    expect(screen.getByText('You have everything')).toBeInTheDocument();
  });

  it('shows the placeholder rather than a card of its own shape when there is no photo', () => {
    const { container } = renderWithProviders(RecipeCard, { props: { recipe: recipe() } });

    expect(container.querySelector('.photo')).toBeInTheDocument();
    expect(container.querySelector('img')).toBeNull();
  });

  it('marks itself busy while a change to it is in flight', () => {
    const { container } = renderWithProviders(RecipeCard, {
      props: { recipe: recipe(), pending: true }
    });

    expect(container.querySelector('article')).toHaveAttribute('aria-busy', 'true');
  });
});
