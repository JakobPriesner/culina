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

  it('keeps what a site says it makes: pieces and its own word for them', () => {
    const recipe = publishedRecipe(
      { ...page, servings: 12, yieldKind: 'pieces', yieldLabel: 'Muffins' },
      []
    );

    expect(recipe.servings).toBe(12);
    expect(recipe.yieldKind).toBe('pieces');
    expect(recipe.yieldLabel).toBe('Muffins');
  });

  it('reads servings with no kind as it always did, and a kind it does not know as servings', () => {
    const plain = publishedRecipe({ ...page, servings: 4 }, []);
    const odd = publishedRecipe({ ...page, servings: 4, yieldKind: 'bowls' }, []);

    expect(plain).not.toHaveProperty('yieldKind');
    expect(plain).not.toHaveProperty('yieldLabel');
    expect(odd).not.toHaveProperty('yieldKind');
  });

  it('says nothing of kind or word without a number', () => {
    const recipe = publishedRecipe(
      { ...page, servings: null, yieldKind: 'pieces', yieldLabel: 'Muffins' },
      []
    );

    expect(recipe).not.toHaveProperty('yieldKind');
    expect(recipe).not.toHaveProperty('yieldLabel');
  });
});
