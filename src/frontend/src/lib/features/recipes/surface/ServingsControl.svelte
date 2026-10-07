<script lang="ts">
  import { Stepper } from '$ds';

  import { m } from '$shell/i18n';
  import { yieldLabel } from '../scaling';
  import type { YieldKind } from '../types';
  import { yieldNoun } from '../yieldWords';

  /**
   * How many this is being made for, usable mid-cook; pieces step by something sensible (twelve
   * muffins by six).
   */
  interface Props {
    value: number;
    kind: YieldKind;
    /**
     * The recipe's own word for what it makes; renames the control only, the step stays with the
     * kind.
     */
    label: string | null;
    base: number;
    onchange?: (value: number) => void;
  }

  let { value = $bindable(), kind, label, base, onchange }: Props = $props();

  const step = $derived(kind === 'pieces' ? stepForPieces(base) : 1);

  /**
   * What the number reads as: scaling to 370 g gives a yield like 7.4 internally, shown as 7½,
   * which is what a person would say.
   */
  const shown = $derived(yieldLabel(value));

  /**
   * Tapping moves to a whole step (not 7.4 to 8.4): someone using the stepper is no longer anchored
   * to an amount.
   */
  const move = (direction: 1 | -1) =>
    onchange?.(
      Math.max(step, (direction > 0 ? Math.floor(shown) : Math.ceil(shown)) + direction * step)
    );

  function stepForPieces(amount: number): number {
    if (amount >= 24) {
      return 12;
    }

    if (amount >= 12) {
      return 6;
    }

    return amount >= 6 ? 2 : 1;
  }
</script>

<Stepper
  id="servings"
  value={shown}
  {step}
  onstep={move}
  min={step}
  max={kind === 'pieces' ? 240 : 24}
  label={yieldNoun({ yieldKind: kind, yieldLabel: label })}
  decreaseLabel={kind === 'pieces' ? m['recipe.pieces.fewer']() : m['recipe.servings.fewer']()}
  increaseLabel={kind === 'pieces' ? m['recipe.pieces.more']() : m['recipe.servings.more']()}
  {onchange}
/>
