import type { Unit } from './units';

/**
 * Shows metric recipes in US kitchen units; display only, nothing reaches the wire, shopping arithmetic or database.
 * Mass never becomes cups (a cup of flour is 120-150 g, so that would be confidently wrong); it becomes ounces and pounds. Spoons and counts stay as they are.
 */
export type MeasurementSystem = 'metric' | 'imperial';

/** The units a US kitchen owns; display only. */
export type CustomaryUnit = 'oz' | 'lb' | 'fl oz' | 'cup';

const customary: Record<CustomaryUnit, true> = {
  oz: true,
  lb: true,
  'fl oz': true,
  cup: true
};

export const isCustomary = (unit: Unit | null | undefined): unit is CustomaryUnit =>
  unit !== null && unit !== undefined && unit in customary;

/** Exact by definition, not rounded constants. */
const gramsPerOunce = 28.349523125;
const gramsPerPound = gramsPerOunce * 16;
const millilitresPerFluidOunce = 29.5735295625;
const millilitresPerCup = millilitresPerFluidOunce * 8;

/** A converted amount, before rounding onto a real measure. */
export interface Converted {
  readonly value: number;
  readonly unit: CustomaryUnit;
  /** The sizes a measure of this unit comes in; rounding lands on these (`⅓ cup`, not `0.31 cup`). */
  readonly steps: readonly number[];
}

/** A mass in grams as ounces below a pound and pounds above, where a recipe changes its word too. */
export function fromGrams(grams: number): Converted {
  const ounces = grams / gramsPerOunce;

  return ounces < 16
    ? { value: ounces, unit: 'oz', steps: quarters }
    : { value: grams / gramsPerPound, unit: 'lb', steps: quarters };
}

/** A volume in ml as fluid ounces below a cup and cups above; no pints, since recipes say "2 cups". */
export function fromMillilitres(millilitres: number): Converted {
  const fluidOunces = millilitres / millilitresPerFluidOunce;

  return fluidOunces < 8
    ? { value: fluidOunces, unit: 'fl oz', steps: quarters }
    : { value: millilitres / millilitresPerCup, unit: 'cup', steps: cupSteps };
}

/** Fractions a measuring spoon or scale shows (quarters), whole numbers included. */
const quarters = [0.25, 0.5, 0.75, 1];

/** Measuring-cup sizes: thirds as well as quarters, since a ⅓ cup exists in every set. */
const cupSteps = [0.25, 1 / 3, 0.5, 2 / 3, 0.75, 1];

/** Rounds onto a measure that exists: fractions up to sixteen, then wholes, then fives ("10½ oz" stays; "11 oz" would be a 4% lie). */
const fractionsUpTo = 16;

export function toMeasure(value: number, steps: readonly number[]): number {
  if (value >= fractionsUpTo) {
    const grid = value >= 50 ? 5 : 1;

    return Math.round(value / grid) * grid;
  }

  const whole = Math.floor(value);
  const fraction = value - whole;

  // Nearer of the steps below and above; the last step supplies the whole number.
  const nearest = [0, ...steps].reduce((best, step) =>
    Math.abs(step - fraction) < Math.abs(best - fraction) ? step : best
  );

  return Number((whole + nearest).toFixed(4));
}
