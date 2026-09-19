import { describe, expect, it } from 'vitest';

import { acceptNothing, toPatch, type Accepted, type Draft } from './draftToRecipe';

import type { Ingredient, Recipe } from '$features/recipes/types';

/** The first item, or a failed test — so an index is never merely assumed. */
const first = <TItem>(items: readonly TItem[] | undefined): TItem => {
  const one = items?.[0];

  if (one === undefined) {
    throw new Error('Expected at least one item.');
  }

  return one;
};

const ingredient = (name: string, id = name): Ingredient => ({
  id,
  quantity: { value: null, unit: null },
  name,
  note: null
});

const recipe = (overrides: Partial<Recipe> = {}): Recipe =>
  ({
    id: 'r1',
    title: 'Soup',
    description: null,
    language: 'en',
    yieldAmount: 4,
    yieldKind: 'servings',
    yieldLabel: null,
    prepMinutes: 10,
    cookMinutes: 20,
    totalMinutes: 30,
    imageId: null,
    groups: [{ id: 'g1', name: null, ingredients: [ingredient('olive oil'), ingredient('onion')] }],
    steps: [],
    tags: [],
    sourceUrl: null,
    updatedAt: '2026-09-19T00:00:00Z',
    householdId: 'h1',
    createdBy: 'u1',
    createdAt: '2026-09-19T00:00:00Z',
    version: 1,
    ...overrides
  }) as Recipe;

const draft = (overrides: Partial<Draft> = {}): Draft => ({
  draftId: '00000000-0000-0000-0000-000000000001',
  title: 'Better soup',
  description: 'A tidier one.',
  yieldAmount: 6,
  yieldLabel: null,
  prepMinutes: 5,
  cookMinutes: 25,
  groups: [{ name: null, ingredients: [{ quantity: 2, unit: 'tbsp', name: 'olive oil' }] }],
  steps: [{ text: 'Heat the olive oil and add the onion.' }],
  tags: [],
  ...overrides
});

describe('accepting parts of a draft', () => {
  const accepting = (parts: Partial<Accepted>): Accepted => ({ ...acceptNothing(), ...parts });

  it('changes nothing at all when nothing is ticked', () => {
    expect(toPatch(draft(), acceptNothing(), recipe())).toEqual({});
  });

  it('takes only the part that was ticked', () => {
    const patch = toPatch(draft(), accepting({ title: true }), recipe());

    expect(patch).toEqual({ title: 'Better soup' });
  });

  it('keeps the current value for a field the draft left blank', () => {
    // Half a suggestion must not be read as "and delete the rest": the draft
    // said a prep time and said nothing about cooking.
    const patch = toPatch(
      draft({ prepMinutes: 5, cookMinutes: null }),
      accepting({ times: true }),
      recipe()
    );

    expect(patch.prepMinutes).toBe(5);
    expect(patch.cookMinutes).toBe(20);
  });

  it('leaves accepted steps as words, linking nothing by itself', () => {
    const patch = toPatch(draft(), accepting({ ingredients: true, steps: true }), recipe());

    // Both ingredient names are in the sentence, and neither becomes a link.
    // The editor stopped guessing which mention was meant on the paste path,
    // and this is the same guess — a wrong link shows a scaled amount inside a
    // sentence that was never about that ingredient.
    const step = first(patch.steps);
    expect(step.segments.map((segment) => segment.kind)).toEqual(['text']);
    expect(step.segments[0]).toEqual({
      kind: 'text',
      text: 'Heat the olive oil and add the onion.'
    });
  });

  it('replaces only the steps when the ingredients were left alone', () => {
    const patch = toPatch(draft(), accepting({ steps: true }), recipe());

    expect(patch.groups).toBeUndefined();
    expect(patch.steps).toHaveLength(1);
  });

  it('flattens the draft groups into the one list the editor shows', () => {
    const patch = toPatch(
      draft({
        groups: [
          { name: 'For the sauce', ingredients: [{ name: 'tomato' }] },
          { name: 'For the top', ingredients: [{ name: 'basil' }] }
        ]
      }),
      accepting({ ingredients: true }),
      recipe()
    );

    expect(patch.groups).toHaveLength(1);
    expect(first(patch.groups).ingredients.map((line) => line.name)).toEqual(['tomato', 'basil']);
  });
});
