import { describe, expect, it } from 'vitest';

import { insertMention, pendingMention, suggest, toSegments, toText } from './mentions';
import type { Ingredient, Step } from '../types';

const ingredient = (id: string, name: string, value: number | null = 200): Ingredient => ({
  id,
  name,
  note: null,
  quantity: { value, unit: value === null ? null : 'g' }
});

const butter = ingredient('i-butter', 'butter');
const oil = ingredient('i-oil', 'oil');
const oliveOil = ingredient('i-olive', 'olive oil');
const list = [butter, oil, oliveOil];

const step = (segments: Step['segments']): Step => ({ id: null, segments, durationSeconds: null });

describe('reading a step back as a sentence', () => {
  it('writes a mention as the author typed it', () => {
    const text = toText(
      step([
        { kind: 'text', text: 'Melt ' },
        { kind: 'ingredient', ingredientId: butter.id, name: 'butter', quantity: butter.quantity },
        { kind: 'text', text: ' in the pan.' }
      ])
    );

    expect(text).toBe('Melt @butter in the pan.');
  });
});

describe('finding the mentions in a sentence', () => {
  it('links a mention to the ingredient it names', () => {
    expect(toSegments('Melt @butter in the pan.', list)).toEqual([
      { kind: 'text', text: 'Melt ' },
      { kind: 'ingredient', ingredientId: butter.id, name: 'butter', quantity: butter.quantity },
      { kind: 'text', text: ' in the pan.' }
    ]);
  });

  it('prefers the longest name, so "olive oil" is not "olive" and a word', () => {
    expect(toSegments('@olive oil', list)).toEqual([
      {
        kind: 'ingredient',
        ingredientId: oliveOil.id,
        name: 'olive oil',
        quantity: oliveOil.quantity
      }
    ]);
  });

  it('leaves a name it does not have as plain words', () => {
    expect(toSegments('Add @saffron.', list)).toEqual([{ kind: 'text', text: 'Add @saffron.' }]);
  });

  it('does not claim the first half of a longer word', () => {
    expect(toSegments('@buttermilk', list)).toEqual([{ kind: 'text', text: '@buttermilk' }]);
  });

  it('ignores an @ that is not pointing at anything', () => {
    expect(toSegments('Bake at 180 °C @ fan.', list)).toEqual([
      { kind: 'text', text: 'Bake at 180 °C @ fan.' }
    ]);
  });

  it('matches however the author capitalised it', () => {
    expect(toSegments('@Butter', list)).toEqual([
      { kind: 'ingredient', ingredientId: butter.id, name: 'butter', quantity: butter.quantity }
    ]);
  });

  it('drops a mention of an ingredient that is no longer in the list', () => {
    expect(toSegments('Melt @butter.', [oil])).toEqual([{ kind: 'text', text: 'Melt @butter.' }]);
  });

  it('cannot reference a line the server has never seen', () => {
    expect(toSegments('Melt @butter.', [{ ...butter, id: '' }])).toEqual([
      { kind: 'text', text: 'Melt @butter.' }
    ]);
  });

  it('survives the round trip through text and back', () => {
    const segments = toSegments('Whisk @olive oil into @butter, then rest.', list);

    expect(toText(step(segments))).toBe('Whisk @olive oil into @butter, then rest.');
  });
});

describe('the mention being typed', () => {
  it('is nothing when the cursor is in ordinary words', () => {
    expect(pendingMention('Melt the butter', 15)).toBeNull();
  });

  it('is an empty query the moment the @ is typed', () => {
    expect(pendingMention('Melt @', 6)).toEqual({ at: 5, query: '' });
  });

  it('grows with what is typed after the @', () => {
    expect(pendingMention('Melt @but', 9)).toEqual({ at: 5, query: 'but' });
  });

  it('keeps going across a space, because names have spaces in them', () => {
    expect(pendingMention('Add @olive oi', 13)).toEqual({ at: 4, query: 'olive oi' });
  });

  it('does not reach back past the end of the line', () => {
    expect(pendingMention('Add @butter\nThen rest', 21)).toBeNull();
  });

  it('belongs to the nearest @, not the first one', () => {
    expect(pendingMention('Add @butter and @oi', 19)).toEqual({ at: 16, query: 'oi' });
  });

  it('is nothing for an @ that starts no word', () => {
    expect(pendingMention('180 °C @ f', 10)).toBeNull();
  });

  it('gives up rather than scanning a whole paragraph', () => {
    expect(pendingMention(`@${'x'.repeat(200)}`, 201)).toBeNull();
  });
});

describe('writing the chosen ingredient in', () => {
  it('replaces what was typed and follows it with a space', () => {
    expect(insertMention('Melt @but', { at: 5, query: 'but' }, 'butter')).toEqual({
      text: 'Melt @butter ',
      caret: 13
    });
  });

  it('does not add a second space when one is already there', () => {
    expect(insertMention('Melt @but in the pan', { at: 5, query: 'but' }, 'butter')).toEqual({
      text: 'Melt @butter in the pan',
      caret: 12
    });
  });

  it('leaves the rest of the sentence where it was', () => {
    expect(insertMention('Melt @ slowly', { at: 5, query: '' }, 'butter')).toEqual({
      text: 'Melt @butter slowly',
      caret: 12
    });
  });
});

describe('what the picker offers', () => {
  it('offers the whole list before anything is typed', () => {
    expect(suggest('', list)).toEqual(list);
  });

  it('puts a name that starts with the query above one that merely contains it', () => {
    expect(suggest('oil', list).map((one) => one.name)).toEqual(['oil', 'olive oil']);
  });

  it('offers nothing for a name the recipe does not have', () => {
    expect(suggest('saffron', list)).toEqual([]);
  });

  it('never offers a line the server has not seen', () => {
    expect(suggest('', [{ ...butter, id: '' }])).toEqual([]);
  });
});
