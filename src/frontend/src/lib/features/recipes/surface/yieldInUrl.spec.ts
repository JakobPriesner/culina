import { describe, expect, it } from 'vitest';

import { urlAtYield, yieldFrom } from './yieldInUrl';
import type { Recipe } from '../types';

const recipe = { yieldAmount: 4 } as Recipe;
const at = (search: string) => new URL(`http://localhost/recipes/r1${search}`);

/*
 * Scaling is a view, so it belongs in the URL: a scaled recipe then survives a
 * reload and can be sent to somebody as the thing you actually meant.
 */
describe('reading the yield out of a URL', () => {
  it('uses the recipe’s own when nothing was asked for', () => {
    expect(yieldFrom(at(''), recipe)).toBe(4);
  });

  it('uses what was asked for', () => {
    expect(yieldFrom(at('?yield=6'), recipe)).toBe(6);
  });

  it.each([['?yield=abc'], ['?yield='], ['?yield=0'], ['?yield=-2']])(
    'falls back rather than trusting %s',
    (search) => {
      expect(yieldFrom(at(search), recipe)).toBe(4);
    }
  );

  it('clamps a number nobody meant', () => {
    expect(yieldFrom(at('?yield=100000'), recipe)).toBe(1000);
  });

  it('has an answer before the recipe has loaded', () => {
    expect(yieldFrom(at('?yield=6'), null)).toBe(6);
    expect(yieldFrom(at(''), null)).toBe(1);
  });
});

describe('writing the yield into a URL', () => {
  it('records a deliberate choice', () => {
    expect(urlAtYield(at(''), 6, recipe)).toBe('/recipes/r1?yield=6');
  });

  it('leaves the plain link plain when it is back to the recipe’s own', () => {
    expect(urlAtYield(at('?yield=6'), 4, recipe)).toBe('/recipes/r1');
  });

  it('keeps whatever else was in the query', () => {
    expect(urlAtYield(at('?from=search'), 6, recipe)).toBe('/recipes/r1?from=search&yield=6');
  });
});
