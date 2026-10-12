import { describe, expect, it } from 'vitest';

import { forAmount } from './nounForms';

describe('a bare count', () => {
  it.each([
    ['Zwiebel', 2, 'Zwiebeln'],
    ['Zwiebeln', 1, 'Zwiebel'],
    ['Zwiebeln', 0.5, 'Zwiebel'],
    ['Apfel', 3, 'Äpfel'],
    ['Ei', 2, 'Eier'],
    ['Knoblauchzehe', 2, 'Knoblauchzehen'],
    ['großer Apfel', 2, 'große Äpfel'],
    ['große Äpfel', 1, 'großer Apfel'],
    ['kleine Eier', 1, 'kleines Ei'],
    ['Zwiebel, rot', 2, 'Zwiebeln, rot']
  ])('writes %s for %d as %s', (name, amount, expected) => {
    expect(forAmount(name, amount, false, 'de')).toBe(expected);
  });

  it('reads English in English', () => {
    expect(forAmount('orange', 2, false, 'en')).toBe('oranges');
    expect(forAmount('garlic clove', 3, false, 'en')).toBe('garlic cloves');
  });
});

describe('a name left as written', () => {
  it.each([
    ['Brot', 2, true],
    ['Spargel', 2, false],
    ['Rührei', 2, false]
  ])('keeps %s', (name, amount, hasUnit) => {
    expect(forAmount(name, amount, hasUnit, 'de')).toBe(name);
  });

  it('keeps a line without an amount', () => {
    expect(forAmount('Zwiebel', null, false, 'de')).toBe('Zwiebel');
  });
});
