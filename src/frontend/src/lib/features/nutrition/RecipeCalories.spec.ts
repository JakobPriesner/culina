import { render, screen } from '@testing-library/svelte';
import { describe, expect, it } from 'vitest';

import type { RecipeSummary } from '$features/recipes/types';

import RecipeCalories from './RecipeCalories.svelte';

/* A card's figure is read next to other cards' figures: it must say what it is for, and when it is only part of the dish. */
const recipe = (over: Partial<RecipeSummary>): RecipeSummary =>
  ({
    id: 'r1',
    title: 'Cake',
    yieldAmount: 4,
    yieldKind: 'servings',
    calories: { value: 480, atLeast: false, estimated: false },
    ...over
  }) as RecipeSummary;

describe('the calories on a recipe card', () => {
  it('says per person for a recipe written for several', () => {
    render(RecipeCalories, { recipe: recipe({}) });

    expect(screen.getByText(/480\skcal per person/)).toBeInTheDocument();
  });

  it('says the figure is for the whole recipe when the recipe makes a single serving', () => {
    render(RecipeCalories, {
      recipe: recipe({
        yieldAmount: 1,
        calories: { value: 3200, atLeast: false, estimated: false }
      })
    });

    expect(screen.getByText(/3,?200\skcal whole recipe/)).toBeInTheDocument();
    expect(screen.queryByText(/per person/)).not.toBeInTheDocument();
  });

  it('says per piece for pieces, whatever their number', () => {
    render(RecipeCalories, { recipe: recipe({ yieldKind: 'pieces', yieldAmount: 1 }) });

    expect(screen.getByText(/480\skcal per piece/)).toBeInTheDocument();
  });

  it('shows a lower bound as one, and not only to a screen reader', () => {
    const { container } = render(RecipeCalories, {
      recipe: recipe({ calories: { value: 18, atLeast: true, estimated: false } })
    });

    expect(container.querySelector('.bound')).toHaveTextContent('≥');
    expect(container.querySelector('.bound')).toHaveAttribute('aria-hidden', 'true');
    expect(container).toHaveTextContent('at least');
  });

  it('shows no bound for a figure that is the whole dish', () => {
    const { container } = render(RecipeCalories, { recipe: recipe({}) });

    expect(container.querySelector('.bound')).not.toBeInTheDocument();
  });
});
