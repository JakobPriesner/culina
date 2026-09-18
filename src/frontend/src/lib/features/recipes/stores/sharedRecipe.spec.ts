import { beforeEach, describe, expect, it, vi } from 'vitest';

import { sharedRecipe } from './sharedRecipe.svelte';

/*
 * The page a stranger lands on. What matters is that it asks under the token
 * and renders the same thing the household's own page does — a recipe whose
 * steps still carry their ingredient references, which is what makes the
 * amounts scale for somebody with no account.
 */
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

    // The surface only asks whether there is a picture; the page hands it the
    // address. Standing in the id's place is what makes that work.
    expect(sharedRecipe.recipe!.imageId).toBe(token);
  });
});
