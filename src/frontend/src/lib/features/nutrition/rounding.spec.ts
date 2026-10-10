import { describe, expect, it } from 'vitest';

import { roundForLabel, type Nutrient } from './rounding';

const exact = (nutrient: Nutrient, value: number) => roundForLabel(nutrient, value, false);
const bound = (nutrient: Nutrient, value: number) => roundForLabel(nutrient, value, true);

describe('energy', () => {
  it('is a whole number, half up when exact', () => {
    expect(exact('energy', 520.4)).toEqual({ value: 520, decimals: 0 });
    expect(exact('energy', 520.5)).toEqual({ value: 521, decimals: 0 });
    expect(exact('energy', 0.4)).toEqual({ value: 0, decimals: 0 });
  });

  it('rounds a lower bound down, however close to the next number', () => {
    expect(bound('energy', 520.9).value).toBe(520);
    expect(bound('energy', 520.999).value).toBe(520);
    expect(bound('energy', 0.9).value).toBe(0);
  });
});

describe('fat, carbohydrate, sugars and protein', () => {
  it('keeps one decimal below 10 g and none from 10 g', () => {
    expect(exact('macro', 9.94)).toEqual({ value: 9.9, decimals: 1 });
    expect(exact('macro', 10)).toEqual({ value: 10, decimals: 0 });
    expect(exact('macro', 10.4)).toEqual({ value: 10, decimals: 0 });
    expect(exact('macro', 10.5)).toEqual({ value: 11, decimals: 0 });
  });

  it('says 0 below 0.5 g and counts 0.5 g itself', () => {
    expect(exact('macro', 0.49)).toEqual({ value: 0, decimals: 1 });
    expect(exact('macro', 0.5)).toEqual({ value: 0.5, decimals: 1 });
  });

  it('rounds half up without floating point drift', () => {
    expect(exact('macro', 1.15)).toEqual({ value: 1.2, decimals: 1 });
    expect(exact('macro', 2.35)).toEqual({ value: 2.4, decimals: 1 });
  });

  it('rounds a lower bound down at either precision', () => {
    expect(bound('macro', 9.99)).toEqual({ value: 9.9, decimals: 1 });
    expect(bound('macro', 10.9)).toEqual({ value: 10, decimals: 0 });
    expect(bound('macro', 3.29)).toEqual({ value: 3.2, decimals: 1 });
    expect(bound('macro', 0.49)).toEqual({ value: 0, decimals: 1 });
    expect(bound('macro', 3.0)).toEqual({ value: 3, decimals: 1 });
    expect(bound('macro', 1.2999999999999998).value).toBe(1.3);
  });
});

describe('saturates', () => {
  it('says 0 below 0.1 g', () => {
    expect(exact('saturates', 0.09)).toEqual({ value: 0, decimals: 1 });
    expect(exact('saturates', 0.1)).toEqual({ value: 0.1, decimals: 1 });
    expect(bound('saturates', 0.099)).toEqual({ value: 0, decimals: 1 });
  });

  it('keeps one decimal below 10 g and none from 10 g', () => {
    expect(exact('saturates', 9.96)).toEqual({ value: 10, decimals: 1 });
    expect(bound('saturates', 9.96)).toEqual({ value: 9.9, decimals: 1 });
    expect(exact('saturates', 12.6)).toEqual({ value: 13, decimals: 0 });
    expect(bound('saturates', 12.6)).toEqual({ value: 12, decimals: 0 });
  });
});

describe('salt', () => {
  it('keeps one decimal from 1 g and two below', () => {
    expect(exact('salt', 1)).toEqual({ value: 1, decimals: 1 });
    expect(exact('salt', 1.26)).toEqual({ value: 1.3, decimals: 1 });
    expect(bound('salt', 1.26)).toEqual({ value: 1.2, decimals: 1 });
    expect(exact('salt', 0.456)).toEqual({ value: 0.46, decimals: 2 });
    expect(bound('salt', 0.456)).toEqual({ value: 0.45, decimals: 2 });
  });

  it('says 0 below 0.0125 g', () => {
    expect(exact('salt', 0.012)).toEqual({ value: 0, decimals: 2 });
    expect(exact('salt', 0.0125)).toEqual({ value: 0.01, decimals: 2 });
    expect(bound('salt', 0.0124)).toEqual({ value: 0, decimals: 2 });
  });
});
