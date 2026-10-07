import { describe, expect, it } from 'vitest';

import { clampYield, factorFor, scaleQuantity, targetYieldForAmount, yieldLabel } from './scaling';
import type { Unit } from './units';

/* The test vectors named in docs/scaling-rules.md. */
const scale = (value: number | null, unit: Unit | null, factor: number) =>
  scaleQuantity({ value, unit }, factor);

describe('mass and volume', () => {
  it.each([
    [7.3, 1, 7.3, 'g'],
    [14.6, 0.5, 7.5, 'g'],
    [100, 1.333, 130, 'g'],
    [50, 1.3, 65, 'g'],
    [325, 1.333, 430, 'g'],
    [1000, 1.333, 1350, 'g'],
    [500, 2, 1, 'kg'],
    [1000, 2, 2, 'kg']
  ])('scales %s g by %s to %s %s', (base, factor, value, unit) => {
    const scaled = scale(base, 'g', factor);

    expect(scaled.value).toBe(value);
    expect(scaled.unit).toBe(unit);
  });

  it.each([
    [9.9, 0.5],
    [10, 5],
    [100, 10],
    [1000, 50]
  ])('uses the step for the magnitude at the boundary %s', (amount, step) => {
    const scaled = scale(amount, 'g', 1.07);
    const inGrams = scaled.unit === 'kg' ? scaled.value! * 1000 : scaled.value!;

    expect(Number((inGrams / step).toFixed(6)) % 1).toBe(0);
  });

  it('re-expresses upward when the number gets unwieldy', () => {
    expect(scale(750, 'g', 2)).toMatchObject({ value: 1.5, unit: 'kg' });
    expect(scale(1000, 'ml', 2)).toMatchObject({ value: 2, unit: 'l' });
  });

  it('never re-expresses downward, because a scale shows grams', () => {
    expect(scale(1, 'kg', 0.5)).toMatchObject({ value: 500, unit: 'g' });
    expect(scale(1, 'l', 0.5)).toMatchObject({ value: 500, unit: 'ml' });
  });

  it('never rounds a small amount to nothing', () => {
    // 0.2 g of saffron is below the half-gram step; zero would drop it and half a gram would be 2.5
    // times it, so it stays exact.
    expect(scale(0.1, 'g', 2)).toMatchObject({ value: 0.2, unit: 'g', isApproximate: false });
    expect(scale(0.4, 'ml', 0.5)).toMatchObject({ value: 0.2, unit: 'ml' });
  });

  it('never converts a small amount to nothing', () => {
    const scaled = scaleQuantity({ value: 2, unit: 'g' }, 1, 'imperial');

    expect(scaled.unit).toBe('oz');
    expect(scaled.value).toBeCloseTo(0.0705, 4);
    expect(scaleQuantity({ value: 3, unit: 'ml' }, 1, 'imperial').value).toBeGreaterThan(0);
  });
});

describe('countable things', () => {
  it('becomes a range rather than a fraction', () => {
    expect(scale(3, 'clove', 1.5)).toMatchObject({ value: 4, upper: 5, isRange: true });
    expect(scale(2, 'piece', 0.67)).toMatchObject({ value: 1, upper: 2, isRange: true });
  });

  it('is exact when the arithmetic is exact', () => {
    expect(scale(4, 'piece', 1.5)).toMatchObject({ value: 6, isRange: false });
  });

  it('snaps when it is within a fraction of a whole one', () => {
    expect(scale(4, 'piece', 0.975)).toMatchObject({ value: 4, isRange: false });
  });

  it('says half an onion when half an onion is what is wanted', () => {
    // Not "1 onion": that would be 2.5 times the onion, silently.
    expect(scale(1, 'piece', 0.5)).toMatchObject({ value: 0.5, isRange: false });
    expect(scale(1, 'piece', 0.4)).toMatchObject({ value: 1 / 3, isRange: false });
    expect(scale(3, 'piece', 0.25)).toMatchObject({ value: 0.75, isRange: false });
  });

  it('never rounds a count to nothing', () => {
    // A quarter is the smallest piece anybody cuts, and an ingredient that disappears is one nobody
    // buys.
    expect(scale(1, 'piece', 0.1)).toMatchObject({ value: 0.25, isApproximate: true });
    expect(scale(1, 'piece', 0.01)).toMatchObject({ value: 0.25 });
  });

  it('treats an amount with no unit as a count', () => {
    expect(scale(3, null, 1.5)).toMatchObject({ value: 4, upper: 5, isRange: true });
  });
});

describe('spoons', () => {
  it('rounds to halves', () => {
    expect(scale(1, 'tbsp', 1.7)).toMatchObject({ value: 1.5, unit: 'tbsp' });
    expect(scale(2, 'tsp', 1.3)).toMatchObject({ value: 2.5, unit: 'tsp' });
  });

  it('allows thirds when the arithmetic produced one, because the spoons exist', () => {
    expect(scale(1, 'tsp', 1 / 3).value).toBeCloseTo(1 / 3, 3);
    expect(scale(1, 'tsp', 2 / 3).value).toBeCloseTo(2 / 3, 3);
  });

  it('keeps a small spoon amount a small spoon amount', () => {
    expect(scale(0.25, 'tsp', 0.5)).toMatchObject({ value: 0.125, isApproximate: false });
    expect(scale(0.5, 'tsp', 0.5)).toMatchObject({ value: 0.25, isApproximate: false });
    expect(scale(0.25, 'tsp', 0.1)).toMatchObject({ value: 0.125, isApproximate: true });
  });

  it('does not use a third as a second grid to round onto', () => {
    expect(scale(1, 'tbsp', 1.7).value).toBe(1.5);
  });

  it('never converts a spoon to millilitres', () => {
    expect(scale(2, 'tbsp', 2).unit).toBe('tbsp');
  });
});

describe('what never scales', () => {
  it('leaves a pinch alone, because salting to taste does not double', () => {
    expect(scale(1, 'pinch', 4)).toMatchObject({ value: 1, unit: 'pinch', isApproximate: false });
  });

  it('leaves an ingredient with no amount alone', () => {
    expect(scale(null, null, 4)).toMatchObject({ value: null, isRange: false });
    expect(scale(null, 'g', 0.25)).toMatchObject({ value: null });
  });
});

describe('a factor of one', () => {
  it('reproduces every amount exactly as authored', () => {
    for (const [value, unit] of [
      [133.33, 'g'],
      [3, 'clove'],
      [1.75, 'tbsp'],
      [1, 'pinch']
    ] as const) {
      expect(scale(value, unit, 1)).toMatchObject({ value, unit, isApproximate: false });
    }
  });
});

describe('marking an approximation', () => {
  it('is not set when rounding barely moved the amount', () => {
    expect(scale(325, 'g', 1.333).isApproximate).toBe(false);
  });

  it('is set when rounding moved the amount by more than 2%', () => {
    expect(scale(7.3, 'g', 1.0001).isApproximate).toBe(true);
    expect(scale(100, 'g', 1.333).isApproximate).toBe(true);
  });

  it('is never set on a range, which states the truth rather than approximating it', () => {
    expect(scale(3, 'clove', 1.5).isApproximate).toBe(false);
  });
});

describe('scaling from an amount you have', () => {
  it('finds the yield that uses it up', () => {
    expect(targetYieldForAmount({ value: 200, unit: 'g' }, { value: 600, unit: 'g' }, 4)).toBe(12);
  });

  it('round-trips: the yield it chose reproduces the amount', () => {
    const base = { value: 200, unit: 'g' } as const;
    const target = targetYieldForAmount(base, { value: 600, unit: 'g' }, 4)!;

    expect(scaleQuantity(base, factorFor(4, target))).toMatchObject({ value: 600, unit: 'g' });
  });

  it('converts within a family', () => {
    expect(targetYieldForAmount({ value: 500, unit: 'g' }, { value: 1, unit: 'kg' }, 4)).toBe(8);
  });

  it('refuses across families, because grams of flour are not millilitres of it', () => {
    expect(
      targetYieldForAmount({ value: 200, unit: 'g' }, { value: 600, unit: 'ml' }, 4)
    ).toBeNull();
  });

  it('refuses on an ingredient with no amount to scale from', () => {
    expect(
      targetYieldForAmount({ value: null, unit: 'g' }, { value: 600, unit: 'g' }, 4)
    ).toBeNull();
  });

  it('keeps the amount somebody actually has, and rounds only the label', () => {
    // 370 g of flour used to become 380 g, with 370 nowhere on screen.
    const target = targetYieldForAmount({ value: 200, unit: 'g' }, { value: 370, unit: 'g' }, 4)!;

    expect(scaleQuantity({ value: 200, unit: 'g' }, factorFor(4, target))).toMatchObject({
      value: 370,
      isApproximate: false
    });

    expect(yieldLabel(target)).toBe(7.5);
  });
});

describe('the factor', () => {
  it('is the ratio of the yields', () => {
    expect(factorFor(4, 6)).toBe(1.5);
    expect(factorFor(4, 2)).toBe(0.5);
  });

  it('is one when the base yield is unusable, rather than infinite', () => {
    expect(factorFor(0, 6)).toBe(1);
  });

  it('clamps a yield nobody meant', () => {
    expect(clampYield(0)).toBeGreaterThan(0);
    expect(clampYield(100_000)).toBe(1000);
  });
});

describe('computing from the base, never from a scaled value', () => {
  it('does not drift however many times the stepper is tapped', () => {
    const base = { value: 333, unit: 'g' } as const;

    const there = scaleQuantity(base, factorFor(4, 10));
    const back = scaleQuantity(base, factorFor(4, 4));

    expect(back.value).toBe(333);
    expect(there.value).not.toBe(333);
  });
});

/*
 * The release-contract fixtures: identity, awkward fractions, amounts that must not move, and every
 * way a yield can arrive broken.
 */
describe('what scaling promises', () => {
  it('changes nothing at all at factor one', () => {
    for (const quantity of [
      { value: 133.333, unit: 'g' },
      { value: 1.7, unit: 'tbsp' },
      { value: 3, unit: 'clove' },
      { value: null, unit: 'g' },
      { value: 1, unit: 'pinch' }
    ] as const) {
      expect(scaleQuantity(quantity, 1)).toMatchObject({
        value: quantity.value,
        isApproximate: false,
        isRange: false
      });
    }
  });

  it('keeps the exact arithmetic beside the readable number', () => {
    const scaled = scaleQuantity({ value: 133, unit: 'g' }, 1 / 3);

    expect(scaled.value).toBe(45);
    expect(scaled.exact).toBeCloseTo(44.3333, 4);
  });

  it('survives a yield that arrived broken', () => {
    // None of these may produce NaN, which would turn every amount on the page into `NaN`.
    for (const [base, target] of [
      [0, 4],
      [-2, 4],
      [Number.NaN, 4],
      [4, Number.NaN],
      [Number.POSITIVE_INFINITY, 4],
      [4, Number.NEGATIVE_INFINITY]
    ]) {
      const factor = factorFor(base!, target!);

      expect(Number.isFinite(factor), `${base} → ${target}`).toBe(true);
      expect(scaleQuantity({ value: 200, unit: 'g' }, factor).value).not.toBeNaN();
    }
  });

  it('does not scale a pinch, or an amount the recipe never gave', () => {
    expect(scaleQuantity({ value: 1, unit: 'pinch' }, 4)).toMatchObject({ value: 1 });
    expect(scaleQuantity({ value: null, unit: null }, 4)).toMatchObject({ value: null });
  });

  it('gives the same answer however many times the stepper was tapped', () => {
    // Scaling an already-scaled (and rounded) value drifts, so every amount is computed from the
    // base and the factor.
    const base = { value: 133, unit: 'g' } as const;
    const once = scaleQuantity(base, factorFor(4, 6));
    const viaEight = scaleQuantity(base, factorFor(4, 8));
    const backToSix = scaleQuantity(base, factorFor(4, 6));

    expect(backToSix).toEqual(once);
    expect(viaEight.value).not.toBe(once.value);
  });

  it('comes home to the authored amounts when the yield goes back', () => {
    const base = { value: 133.333, unit: 'g' } as const;

    expect(scaleQuantity(base, factorFor(4, 4))).toMatchObject({ value: 133.333 });
  });
});
