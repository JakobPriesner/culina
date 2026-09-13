<script lang="ts">
  import { Stepper } from '$ds';

  import { m } from '$shell/i18n';
  import { yieldLabel } from '../scaling';
  import type { YieldKind } from '../types';

  /**
   * How many this is being made for.
   *
   * It sits in the same place whether you are reading or cooking, and stays
   * usable mid-cook — realising halfway through that one more person is coming
   * is exactly when you need it.
   *
   * Pieces step by something that suits the recipe: twelve muffins go up by
   * six, not by one, because nobody bakes thirteen.
   */
  interface Props {
    value: number;
    kind: YieldKind;
    /** The recipe's own yield, which decides a sensible step for pieces. */
    base: number;
    onchange?: (value: number) => void;
  }

  let { value = $bindable(), kind, base, onchange }: Props = $props();

  const step = $derived(kind === 'pieces' ? stepForPieces(base) : 1);

  /**
   * What the number reads as, which is not always what it is.
   *
   * Scaling to an amount somebody has — 370 g of that flour — produces a yield
   * like 7.4. The amounts are computed from 7.4, because that is what makes the
   * flour come out at 370 g; the control says 7½, because that is a number
   * somebody would say aloud.
   */
  const shown = $derived(yieldLabel(value));

  /**
   * Tapping moves to a whole step, not 7.4 to 8.4.
   *
   * Somebody who has left the exact yield behind by touching the stepper is no
   * longer anchored to an amount, and wants the ordinary numbers back.
   */
  const move = (direction: 1 | -1) =>
    onchange?.(
      Math.max(step, (direction > 0 ? Math.floor(shown) : Math.ceil(shown)) + direction * step)
    );

  /** Half a batch, rounded to something whole, and never zero. */
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
  label={kind === 'pieces' ? m['recipe.pieces.label']() : m['recipe.servings.label']()}
  decreaseLabel={kind === 'pieces' ? m['recipe.pieces.fewer']() : m['recipe.servings.fewer']()}
  increaseLabel={kind === 'pieces' ? m['recipe.pieces.more']() : m['recipe.servings.more']()}
  {onchange}
/>
