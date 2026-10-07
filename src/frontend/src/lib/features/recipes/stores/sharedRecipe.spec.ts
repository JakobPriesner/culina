import { beforeEach, describe, expect, it, vi } from 'vitest';

import { sharedRecipe } from './sharedRecipe.svelte';

/* The stranger's page: asks under the token and renders what the household page does, steps keeping ingredient references so amounts scale. */
const token = 'a-token';

const body = {
  title: 'Lemon orzo',
  language: 'en',
  yieldAmount: 2,
  yieldKind: 'servings',
  hasImage: true,
  tags: ['Weeknight'],
  groups: [
    {
      groupId: 'g1',
      ingredients: [{ ingredientId: 'i1', quantity: 200, unit: 'g', name: 'butter' }]
    }
  ],
  steps: [
    {
      stepId: 's1',
      segments: [
        { type: 'text', value: 'Melt ' },
        { type: 'ingredient', recipeIngredientId: 'i1', name: 'butter', quantity: 200, unit: 'g' }
      ],
      uses: ['i1']
    }
  ]
};

let asked: string[] = [];

beforeEach(() => {
  asked = [];
  vi.stubGlobal(
    'fetch',
    vi.fn((input: Request) => {
      asked.push(input.url);

      return Promise.resolve(
        new Response(JSON.stringify(body), {
          status: 200,
          headers: { 'Content-Type': 'application/json' }
        })
      );
    })
  );
});

describe('sharedRecipe', () => {
  it('asks under the token, never under a recipe id', async () => {
    await sharedRecipe.load(token);

    expect(asked[0]).toContain(`/api/v1/shared-recipes/${token}`);
  });

  it('keeps the ingredient references, so the amounts still scale', async () => {
    await sharedRecipe.load(token);

    const segments = sharedRecipe.recipe!.steps[0]!.segments;

    expect(segments[1]).toEqual({
      kind: 'ingredient',
      ingredientId: 'i1',
      name: 'butter',
      quantity: { value: 200, unit: 'g' }
    });
  });

  it('points the photograph at the token, because there is no recipe id here', async () => {
    await sharedRecipe.load(token);

    // The surface only asks whether there is a picture; the token stands in for the id so the page can hand it the address.
    expect(sharedRecipe.recipe!.imageId).toBe(token);
  });
});
