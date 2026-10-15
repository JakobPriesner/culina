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
  it('says per serving for a recipe written for several', () => {
    const { container } = render(RecipeCalories, { recipe: recipe({}) });

    expect(container.querySelector('.calories')).toHaveTextContent(/480\skcal per serving/);
  });

  it('says the figure is for the whole recipe when the recipe makes a single serving', () => {
    const { container } = render(RecipeCalories, {
      recipe: recipe({
        yieldAmount: 1,
        calories: { value: 3200, atLeast: false, estimated: false }
      })
    });

    expect(container.querySelector('.calories')).toHaveTextContent(/3,?200\skcal whole recipe/);
    expect(container.querySelector('.calories')).not.toHaveTextContent(/per serving/);
  });

  it('says per piece for pieces, whatever their number', () => {
    const { container } = render(RecipeCalories, {
      recipe: recipe({ yieldKind: 'pieces', yieldAmount: 1 })
    });

    expect(container.querySelector('.calories')).toHaveTextContent(/480\skcal per piece/);
  });

  it('shows no number when unknown food outweighs what was counted', () => {
    render(RecipeCalories, {
      recipe: recipe({ calories: { value: 139, atLeast: true, mostlyMissing: true } })
    });

    expect(screen.getByText('calories incomplete')).toBeInTheDocument();
    expect(screen.queryByText(/139/)).not.toBeInTheDocument();
  });

  it('shows a lower bound as one, in a word the eye and a screen reader both read', () => {
    const { container } = render(RecipeCalories, {
      recipe: recipe({ calories: { value: 18, atLeast: true, estimated: true } })
    });

    // The bound wins: "at least about 18" says less than it costs; the estimate is in the explanation.
    expect(container.querySelector('.calories')).toHaveTextContent(
      /^at least\s18\skcal per serving/
    );
    expect(container.querySelector('.qualifier')).not.toHaveAttribute('aria-hidden');
  });

  it('says about for an estimate that is not a bound', () => {
    const { container } = render(RecipeCalories, {
      recipe: recipe({ calories: { value: 480, atLeast: false, estimated: true } })
    });

    expect(container.querySelector('.calories')).toHaveTextContent(/^about\s480\skcal per serving/);
  });

  it('shows no bound for a figure that is the whole dish', () => {
    const { container } = render(RecipeCalories, { recipe: recipe({}) });

    expect(container.querySelector('.bound')).not.toBeInTheDocument();
  });
});
