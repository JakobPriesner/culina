<script lang="ts">
  import { Stepper } from '$ds';

  import { m } from '$shell/i18n';
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
  bind:value
  {step}
  min={step}
  max={kind === 'pieces' ? 240 : 24}
  label={kind === 'pieces' ? m['recipe.pieces.label']() : m['recipe.servings.label']()}
  decreaseLabel={kind === 'pieces' ? m['recipe.pieces.fewer']() : m['recipe.servings.fewer']()}
  increaseLabel={kind === 'pieces' ? m['recipe.pieces.more']() : m['recipe.servings.more']()}
  {onchange}
/>
