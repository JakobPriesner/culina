import { describe, expect, it } from 'vitest';

import type { ParsedRecipe } from '$features/recipes/editor/parseRecipeText';

import { recipePatchFromPaste } from './recipePatchFromPaste';

const parsed: ParsedRecipe = { title: 'Muffins', ingredients: [], steps: [] };

describe('what a pasted or imported recipe makes', () => {
  it('becomes pieces with their word when the page said "12 Muffins"', () => {
    const patch = recipePatchFromPaste({
      ...parsed,
      servings: 12,
      yieldKind: 'pieces',
      yieldLabel: 'Muffins'
    });

    expect(patch).toMatchObject({ yieldAmount: 12, yieldKind: 'pieces', yieldLabel: 'Muffins' });
  });

  it('stays servings without a kind', () => {
    expect(recipePatchFromPaste({ ...parsed, servings: 4 })).toMatchObject({
      yieldAmount: 4,
      yieldKind: 'servings',
      yieldLabel: null
    });
  });

  it('leaves the yield alone when nothing was said', () => {
    const patch = recipePatchFromPaste(parsed);

    expect(patch).not.toHaveProperty('yieldAmount');
    expect(patch).not.toHaveProperty('yieldKind');
  });
});
