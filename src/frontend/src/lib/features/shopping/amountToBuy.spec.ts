import { describe, expect, it } from 'vitest';

import { formatQuantity } from '$features/recipes/formatQuantity';
import { quantityLabels } from '$features/recipes/quantityLabels';

import { amountToBuy } from './amountToBuy';

const shown = (value: number | null, unit: string | null, locale = 'en') =>
  formatQuantity(amountToBuy({ value, unit }, 'metric'), locale, quantityLabels).text.replace(
    /\u00a0/g,
    ' '
  );

/* A list is read while walking, so it says what could be put in a basket; the exact sum stays on the server. */
describe('the amount to buy', () => {
  it.each([
    [1108.33, 'g', '1.1 kg'],
    [2116.67, 'g', '2.1 kg'],
    [1916.67, 'ml', '1.9 l'],
    [608.33, 'g', '610 g'],
    [250, 'g', '250 g'],
    [37.5, 'g', '~40 g']
  ])('weighs %f %s as %s', (value, unit, text) => {
    expect(shown(value, unit)).toBe(text);
  });

  it('puts a spoon on a spoon size', () => {
    expect(shown(0.42, 'tsp')).toBe('~½ tsp');
  });

  it.each([
    [1.67, 'pack', '~2'],
    [2.5, 'piece', '~3'],
    [3, 'piece', '3'],
    [3.04, null, '3'],
    [0.42, 'pack', '~1']
  ])('buys %f %s as %s whole', (value, unit, text) => {
    expect(shown(value, unit).replace(/ \p{L}+$/u, '')).toBe(text);
  });

  it('never says a range for something that is bought whole', () => {
    expect(shown(2.5, null)).not.toContain('–');
  });

  it('leaves a line with no amount with none', () => {
    expect(shown(null, null)).toBe('');
  });

  it('keeps a pinch a pinch', () => {
    expect(shown(2, 'pinch')).toBe('2 pinch');
  });
});
