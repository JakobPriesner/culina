import { describe, expect, it } from 'vitest';

import { moved, secondsFromMinutes } from './stepEdits';

describe('a step timer written in minutes', () => {
  it('is stored as seconds', () => {
    expect(secondsFromMinutes('10')).toBe(600);
    expect(secondsFromMinutes('0.5')).toBe(30);
  });

  it('is no timer when empty, unreadable or not above zero', () => {
    expect(secondsFromMinutes('')).toBeNull();
    expect(secondsFromMinutes('  ')).toBeNull();
    expect(secondsFromMinutes('soon')).toBeNull();
    expect(secondsFromMinutes('0')).toBeNull();
    expect(secondsFromMinutes('-5')).toBeNull();
  });

  it('never runs for longer than a day', () => {
    expect(secondsFromMinutes('99999')).toBe(86_400);
  });
});

describe('moving an item in a list', () => {
  it('moves it by the given number of places', () => {
    expect(moved(['a', 'b', 'c'], 0, 1)).toEqual(['b', 'a', 'c']);
    expect(moved(['a', 'b', 'c'], 2, -1)).toEqual(['a', 'c', 'b']);
  });

  it('leaves the list alone at either end', () => {
    const list = ['a', 'b'];

    expect(moved(list, 0, -1)).toBe(list);
    expect(moved(list, 1, 1)).toBe(list);
  });
});
