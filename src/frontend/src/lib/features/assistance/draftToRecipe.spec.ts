import { describe, expect, it } from 'vitest';

import { acceptNothing, toPatch, withMentions, type Accepted, type Draft } from './draftToRecipe';

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

describe('marking ingredient names in a step', () => {
  it('finds the longest name first, so "olive oil" is one ingredient not two', () => {
    const marked = withMentions('Heat the olive oil.', [
      ingredient('oil'),
      ingredient('olive oil')
    ]);

    expect(marked).toBe('Heat the @olive oil.');
  });

  it('only matches a whole word, so "oil" is not found inside "boiling"', () => {
    const marked = withMentions('Bring to a boiling point.', [ingredient('oil')]);

    expect(marked).toBe('Bring to a boiling point.');
  });

  it('matches whatever case the sentence used', () => {
    const marked = withMentions('Onion goes in first.', [ingredient('onion')]);

    expect(marked).toBe('@Onion goes in first.');
  });

  it('leaves a sentence alone when it names nothing in the list', () => {
    expect(withMentions('Season and serve.', [ingredient('onion')])).toBe('Season and serve.');
  });
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

  it('links accepted steps against the accepted ingredients', () => {
    const patch = toPatch(draft(), accepting({ ingredients: true, steps: true }), recipe());

    // "olive oil" is in the new list, so it becomes a reference the app can
    // scale. "onion" is not, so it stays as words.
    const step = first(patch.steps);
    expect(step.segments.map((segment) => segment.kind)).toContain('ingredient');
    expect(step.segments.filter((one) => one.kind === 'ingredient')).toHaveLength(1);
  });

  it('links accepted steps against the existing ingredients when the list was not accepted', () => {
    const patch = toPatch(draft(), accepting({ steps: true }), recipe());

    // Both names are in the recipe as it stands, so both are found — which is
    // the point of choosing the list the steps will actually sit beside.
    expect(patch.groups).toBeUndefined();
    expect(first(patch.steps).segments.filter((one) => one.kind === 'ingredient')).toHaveLength(2);
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
