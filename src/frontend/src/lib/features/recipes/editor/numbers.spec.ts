import { describe, expect, it } from 'vitest';

import {
  minutesFrom,
  minutesShown,
  minutesWrong,
  nothingTyped,
  numberIn,
  totalMinutes,
  yieldFrom,
  yieldShown,
  yieldWrong
} from './numbers';

describe('numberIn', () => {
  it.each([
    ['12', 12],
    ['1,5', 1.5],
    ['1.5', 1.5],
    [' 3 ', 3]
  ])('reads %j as %d', (text, expected) => {
    expect(numberIn(text)).toBe(expected);
  });

  it.each(['', '  ', 'abc', '1,x'])('refuses %j', (text) => {
    expect(numberIn(text)).toBeNull();
  });
});

describe('yield', () => {
  it('accepts only a positive number', () => {
    expect(yieldFrom('4')).toBe(4);
    expect(yieldFrom('0')).toBeNull();
    expect(yieldFrom('-2')).toBeNull();
    expect(yieldFrom('')).toBeNull();
  });

  it('is wrong exactly when it cannot be written', () => {
    expect(yieldWrong('0')).toBe(true);
    expect(yieldWrong('2,5')).toBe(false);
  });

  it('shows what was typed before what the recipe says', () => {
    expect(yieldShown(nothingTyped(), 4)).toBe('4');
    expect(yieldShown({ ...nothingTyped(), yieldAmount: '' }, 4)).toBe('');
  });
});

describe('minutes', () => {
  it('clears on blank, rounds a number and ignores the rest', () => {
    expect(minutesFrom('  ')).toBeNull();
    expect(minutesFrom('12,6')).toBe(13);
    expect(minutesFrom('0')).toBe(0);
    expect(minutesFrom('-1')).toBeUndefined();
    expect(minutesFrom('soon')).toBeUndefined();
  });

  it('is wrong for text that is neither blank nor a time', () => {
    expect(minutesWrong('')).toBe(false);
    expect(minutesWrong('10')).toBe(false);
    expect(minutesWrong('-1')).toBe(true);
    expect(minutesWrong('soon')).toBe(true);
  });

  it('shows typed text, then the saved number, then nothing', () => {
    expect(minutesShown({ ...nothingTyped(), prepMinutes: '1,' }, 'prepMinutes', 5)).toBe('1,');
    expect(minutesShown(nothingTyped(), 'prepMinutes', 5)).toBe('5');
    expect(minutesShown(nothingTyped(), 'cookMinutes', null)).toBe('');
  });
});

describe('totalMinutes', () => {
  it('adds the two halves and says nothing for zero', () => {
    expect(totalMinutes(10, 20)).toBe(30);
    expect(totalMinutes(null, 20)).toBe(20);
    expect(totalMinutes(null, null)).toBeNull();
    expect(totalMinutes(0, 0)).toBeNull();
  });
});
