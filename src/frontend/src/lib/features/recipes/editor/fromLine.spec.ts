import { describe, expect, it } from 'vitest';

import { emptyDraft, fromLine } from './IngredientFields.svelte';

describe('reading a whole line typed into the name field', () => {
  it('splits amount, unit, name and preparation', () => {
    expect(fromLine({ ...emptyDraft, name: '200 g Mehl, gesiebt' })).toEqual({
      amount: '200',
      unit: 'g',
      name: 'Mehl',
      note: 'gesiebt'
    });
  });

  it('reads a count without a unit', () => {
    expect(fromLine({ ...emptyDraft, name: '2 Zucchini' })).toMatchObject({
      amount: '2',
      unit: '',
      name: 'Zucchini'
    });
  });

  it('leaves a name without an amount alone, comma and all', () => {
    const draft = { ...emptyDraft, name: 'Salz, eine Prise' };

    expect(fromLine(draft)).toBe(draft);
  });

  it('never overrides parts typed separately', () => {
    const draft = { ...emptyDraft, amount: '3', name: '200 g Mehl' };

    expect(fromLine(draft)).toBe(draft);
  });

  it('keeps a note typed in its own field', () => {
    expect(fromLine({ ...emptyDraft, name: '1 Zwiebel, gewürfelt', note: 'rot' }).note).toBe('rot');
  });
});
