import { describe, expect, it } from 'vitest';

import { combineIngredients } from './ingredientLines';
import type { Ingredient } from './types';

const ingredient = (one: Partial<Ingredient> & { name: string }): Ingredient => ({
  id: one.name,
  quantity: { value: null, unit: null },
  note: null,
  ...one
});

describe('adding up an ingredient list', () => {
  it('leaves a list with nothing repeated exactly as it was written', () => {
    const lines = combineIngredients([
      ingredient({ name: 'butter', quantity: { value: 200, unit: 'g' } }),
      ingredient({ name: 'flour', quantity: { value: 300, unit: 'g' } })
    ]);

    expect(lines.map((line) => line.name)).toEqual(['butter', 'flour']);
    expect(lines[0]?.quantity).toEqual({ value: 200, unit: 'g' });
  });

  it('adds the same thing together, wherever the recipe asked for it', () => {
    const lines = combineIngredients([
      ingredient({ id: 'a', name: 'butter', quantity: { value: 50, unit: 'g' } }),
      ingredient({ id: 'b', name: 'flour', quantity: { value: 300, unit: 'g' } }),
      ingredient({ id: 'c', name: 'butter', quantity: { value: 30, unit: 'g' } })
    ]);

    expect(lines).toHaveLength(2);
    expect(lines[0]?.quantity).toEqual({ value: 80, unit: 'g' });
    // Where it was first asked for, not at the bottom: the order is the
    // recipe's own, and it is the order the steps beside it are written in.
    expect(lines[0]?.name).toBe('butter');
  });

  it('keeps every id, so a step can still point at what it folded into', () => {
    const lines = combineIngredients([
      ingredient({ id: 'a', name: 'butter', quantity: { value: 50, unit: 'g' } }),
      ingredient({ id: 'b', name: 'butter', quantity: { value: 30, unit: 'g' } })
    ]);

    expect(lines[0]?.ids).toEqual(['a', 'b']);
  });

  it('converts within a family, and keeps the unit the recipe chose', () => {
    const lines = combineIngredients([
      ingredient({ id: 'a', name: 'flour', quantity: { value: 1, unit: 'kg' } }),
      ingredient({ id: 'b', name: 'flour', quantity: { value: 500, unit: 'g' } })
    ]);

    expect(lines[0]?.quantity).toEqual({ value: 1.5, unit: 'kg' });
  });

  it('leaves amounts that cannot be added at all on their own lines', () => {
    const lines = combineIngredients([
      ingredient({ id: 'a', name: 'oil', quantity: { value: 100, unit: 'ml' } }),
      ingredient({ id: 'b', name: 'oil', quantity: { value: 1, unit: 'tbsp' } })
    ]);

    // A tablespoon of oil is not a number of millilitres — how many depends on
    // the oil and on the spoon. Two lines is the honest answer.
    expect(lines).toHaveLength(2);
  });

  it('matches names however they were typed', () => {
    const lines = combineIngredients([
      ingredient({ id: 'a', name: 'Butter', quantity: { value: 50, unit: 'g' } }),
      ingredient({ id: 'b', name: 'butter ', quantity: { value: 50, unit: 'g' } })
    ]);

    expect(lines).toHaveLength(1);
    expect(lines[0]?.name).toBe('Butter');
  });

  it('adds nothing for an ingredient the recipe gives no amount for', () => {
    const lines = combineIngredients([
      ingredient({ id: 'a', name: 'salt' }),
      ingredient({ id: 'b', name: 'salt' })
    ]);

    expect(lines).toHaveLength(1);
    expect(lines[0]?.quantity.value).toBeNull();
  });

  it('keeps both preparations, because each was written beside an amount', () => {
    const lines = combineIngredients([
      ingredient({ id: 'a', name: 'onion', quantity: { value: 100, unit: 'g' }, note: 'diced' }),
      ingredient({ id: 'b', name: 'onion', quantity: { value: 200, unit: 'g' }, note: 'in rings' })
    ]);

    expect(lines[0]?.note).toBe('diced, in rings');
  });

  it('says a preparation once when both lines said the same one', () => {
    const lines = combineIngredients([
      ingredient({ id: 'a', name: 'onion', quantity: { value: 100, unit: 'g' }, note: 'diced' }),
      ingredient({ id: 'b', name: 'onion', quantity: { value: 200, unit: 'g' }, note: 'diced' })
    ]);

    expect(lines[0]?.note).toBe('diced');
  });

  it('adds before anything is scaled, so no rounding is added twice', () => {
    const lines = combineIngredients([
      ingredient({ id: 'a', name: 'butter', quantity: { value: 0.1, unit: 'kg' } }),
      ingredient({ id: 'b', name: 'butter', quantity: { value: 0.2, unit: 'kg' } })
    ]);

    // 0.1 + 0.2, without the floating-point tail that would reach the screen.
    expect(lines[0]?.quantity.value).toBe(0.3);
  });
});
