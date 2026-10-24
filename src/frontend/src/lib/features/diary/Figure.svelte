<script lang="ts">
  import { VisuallyHidden } from '$ds';
  import { nbsp } from '$features/nutrition/format';

  import { kcalNumber, precisionMark, precisionOf, precisionWord } from './format';

  /**
   * An energy figure with how far to trust it (docs/glossary.md): a light ≈ or ≥ beside the number, read aloud as
   * "etwa" or "mindestens", instead of a word before every figure on the page.
   */
  interface Props {
    kcal: number;
    atLeast?: boolean;
    estimated?: boolean;
    /** Says "kcal" after it; a column of figures under one heading does not need it. */
    unit?: boolean;
  }

  let { kcal, atLeast = false, estimated = false, unit = true }: Props = $props();

  const precision = $derived(precisionOf({ atLeast, estimated }));
</script>

<span class="figure"
  >{#if precisionMark[precision]}<span class="mark" aria-hidden="true"
      >{precisionMark[precision]}</span
    ><VisuallyHidden>{precisionWord(precision)}{nbsp}</VisuallyHidden>{/if}{kcalNumber(
    kcal,
    atLeast
  )}{#if unit}<span class="unit">{nbsp}kcal</span>{/if}</span
>

<style>
  .figure {
    font-variant-numeric: tabular-nums;
    white-space: nowrap;
  }

  .mark {
    margin-inline-end: 0.15em;
    color: var(--text-subtle);
    font-weight: var(--weight-regular);
  }

  .unit {
    font-size: 0.85em;
  }
</style>
