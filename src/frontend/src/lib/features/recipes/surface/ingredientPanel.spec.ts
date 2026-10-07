import { beforeEach, describe, expect, it } from 'vitest';

import { createIngredientPanel } from './ingredientPanel.svelte';
import type { Ingredient, RecipeReading, Step } from '../types';

const line = (id: string): Ingredient => ({
  id,
  name: id,
  quantity: { value: null, unit: null },
  note: null
});

const step = (id: string, uses: string[]) => ({ id, uses }) as unknown as Step;

/** Only the groups and steps are read, so only they are built. */
const recipeOf = (steps: Step[]) =>
  ({
    groups: [{ id: 'g', name: null, ingredients: [line('butter'), line('flour'), line('salt')] }],
    steps
  }) as unknown as RecipeReading;

const panelFor = (steps: Step[], cooking = false, currentStep = 0) =>
  createIngredientPanel({
    recipe: () => recipeOf(steps),
    cooking: () => cooking,
    currentStep: () => currentStep
  });

const names = (lines: readonly Ingredient[]) => lines.map((one) => one.name);

describe('the ingredient panel', () => {
  beforeEach(() => localStorage.clear());

  it('holds the whole list when reading it combined', () => {
    const panel = panelFor([step('a', ['butter']), step('b', ['flour'])]);

    expect(names(panel.panel)).toEqual(['butter', 'flour', 'salt']);
    expect(panel.perStep).toBe(false);
  });

  it('keeps only what no step names once the list is dealt out by step', () => {
    const panel = panelFor([step('a', ['butter']), step('b', ['flour'])]);

    panel.choose('perStep');

    expect(panel.perStep).toBe(true);
    expect(names(panel.panel)).toEqual(['salt']);
  });

  it('reads a lone step side by side whichever arrangement was chosen', () => {
    const panel = panelFor([step('a', ['butter'])]);

    panel.choose('perStep');

    expect(panel.view).toBe('perStep');
    expect(panel.divisible).toBe(false);
    expect(panel.perStep).toBe(false);
  });

  it('holds only the current step needs while cooking, dropping ids the recipe lost', () => {
    const panel = panelFor([step('a', ['butter']), step('b', ['flour', 'gone'])], true, 1);

    panel.choose('perStep');

    expect(panel.perStep).toBe(false);
    expect(names(panel.panel)).toEqual(['flour']);
  });

  it('remembers the arrangement for the next recipe', () => {
    panelFor([step('a', ['butter'])]).choose('perStep');

    expect(panelFor([step('a', [])]).view).toBe('perStep');
  });
});
