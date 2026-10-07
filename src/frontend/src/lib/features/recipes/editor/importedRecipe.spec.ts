import { describe, expect, it } from 'vitest';

import { publishedRecipe } from './importedRecipe';

const page = {
  sourceUrl: 'https://example.test/soup',
  title: 'Soup',
  ingredientLines: ['2 carrots', '1 l water'],
  steps: ['Chop.', 'Boil.']
};

describe('a recipe a website published', () => {
  it('reads every ingredient line and keeps the steps and the address', () => {
    const recipe = publishedRecipe(page, []);

    expect(recipe.sourceUrl).toBe(page.sourceUrl);
    expect(recipe.title).toBe('Soup');
    expect(recipe.ingredients.map((ingredient) => ingredient.name)).toEqual(['carrots', 'water']);
    expect(recipe.steps).toEqual(['Chop.', 'Boil.']);
  });

  it('does not invent what the site did not say', () => {
    const recipe = publishedRecipe({ ...page, title: null, servings: null }, []);

    expect(recipe.title).toBe('');
    expect(recipe).not.toHaveProperty('servings');
    expect(recipe).not.toHaveProperty('totalMinutes');
  });

  it('keeps servings and time when the site stated them', () => {
    const recipe = publishedRecipe({ ...page, servings: 4, totalMinutes: 30 }, []);

    expect(recipe.servings).toBe(4);
    expect(recipe.totalMinutes).toBe(30);
  });
});
