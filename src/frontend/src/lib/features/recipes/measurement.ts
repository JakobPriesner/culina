import type { Unit } from './units';

/**
 * Showing a metric recipe in the units a US kitchen owns.
 *
 * Presentation, entirely. A recipe is stored in whatever its author wrote, and
 * nothing here reaches the wire, the shopping list's arithmetic or the
 * database — the same reasoning that puts rounding on this side of the network
 * (`docs/scaling-rules.md`).
 *
 * **Mass never becomes cups.** A cup is a volume, and "1 cup of flour" is
 * anywhere between 120 g and 150 g depending on how it was packed and whether
 * it was sifted. Converting 250 g of flour into "2 cups" produces a
 * confidently wrong recipe, which is worse than an unfamiliar one — so mass
 * becomes ounces and pounds, which are the same measurement said differently
 * and cannot be wrong.
 *
 * **Spoons stay spoons.** A teaspoon is a teaspoon in both systems, and the
 * app already refuses to convert them to millilitres for the same reason.
 *
 * **Counts stay counts.** Two onions are two onions.
 */
export type MeasurementSystem = 'metric' | 'imperial';

/** The units a US kitchen actually owns. Display only: these never travel. */
export type CustomaryUnit = 'oz' | 'lb' | 'fl oz' | 'cup';

const customary: Record<CustomaryUnit, true> = {
  oz: true,
  lb: true,
  'fl oz': true,
  cup: true
};

/** Whether this is one of the display units a conversion produces. */
export const isCustomary = (unit: Unit | null | undefined): unit is CustomaryUnit =>
  unit !== null && unit !== undefined && unit in customary;

/** Exact by definition, both of them. Nothing here is a rounded constant. */
const gramsPerOunce = 28.349523125;
const gramsPerPound = gramsPerOunce * 16;
const millilitresPerFluidOunce = 29.5735295625;
const millilitresPerCup = millilitresPerFluidOunce * 8;

/** A converted amount, before it is rounded onto anything a kitchen owns. */
export interface Converted {
  readonly value: number;
  readonly unit: CustomaryUnit;
  /**
   * The sizes a measure of this unit comes in.
   *
   * Rounding is onto these and nothing else: a cup that reads `0.31 cup` is a
   * number, and `⅓ cup` is a thing in the drawer.
   */
  readonly steps: readonly number[];
}

/**
 * A mass in grams, as a US kitchen would weigh it.
 *
 * Ounces below a pound, pounds above it — which is where a recipe changes its
 * word too: nobody writes "24 oz of beef".
 */
export function fromGrams(grams: number): Converted {
  const ounces = grams / gramsPerOunce;

  return ounces < 16
    ? { value: ounces, unit: 'oz', steps: quarters }
    : { value: grams / gramsPerPound, unit: 'lb', steps: quarters };
}

/**
 * A volume in millilitres, as a US kitchen would measure it.
 *
 * Fluid ounces below a cup and cups above it. A pint is deliberately absent:
 * recipes say "2 cups", and a cook holding a measuring cup would have to
 * convert it back.
 */
export function fromMillilitres(millilitres: number): Converted {
  const fluidOunces = millilitres / millilitresPerFluidOunce;

  return fluidOunces < 8
    ? { value: fluidOunces, unit: 'fl oz', steps: quarters }
    : { value: millilitres / millilitresPerCup, unit: 'cup', steps: cupSteps };
}

/**
 * The fractions a measuring spoon or a scale can show: quarters.
 *
 * Whole numbers included, because most amounts land on one.
 */
const quarters = [0.25, 0.5, 0.75, 1];

/**
 * The sizes a set of measuring cups comes in.
 *
 * Thirds as well as quarters, because a ⅓-cup measure is in every set — which
 * is exactly why a cup amount must not be rounded to quarters alone.
 */
const cupSteps = [0.25, 1 / 3, 0.5, 2 / 3, 0.75, 1];

/**
 * Rounds onto a measure that exists, at a scale that suits the number.
 *
 * Fractions up to sixteen, which is where an ounce becomes a pound and a cup
 * count stops being something anyone measures out one at a time. "10½ oz" is
 * what a recipe says; rounding it to "11 oz" is a four per cent lie for no
 * gain. Above that, whole ones and then fives.
 */
const fractionsUpTo = 16;

export function toMeasure(value: number, steps: readonly number[]): number {
  if (value >= fractionsUpTo) {
    const grid = value >= 50 ? 5 : 1;

    return Math.round(value / grid) * grid;
  }

  const whole = Math.floor(value);
  const fraction = value - whole;

  // The step below and the step above, and whichever of them is nearer. A
  // fraction of nought takes the whole number, which the last step supplies.
  const nearest = [0, ...steps].reduce((best, step) =>
    Math.abs(step - fraction) < Math.abs(best - fraction) ? step : best
  );

  return Number((whole + nearest).toFixed(4));
}
