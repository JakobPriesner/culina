<script lang="ts">
  import type { Scaling } from './scaled.svelte';
  import type { IngredientLine } from '../ingredientLines';

  /**
   * One line of the ingredient list; the amount is its own element so it can be highlighted, and
   * the columns come from `IngredientList`.
   */
  interface Props {
    line: IngredientLine;
    scaling: Scaling;
    highlighted?: boolean;
    onhover?: (ids: readonly string[] | null) => void;
  }

  let { line, scaling, highlighted = false, onhover }: Props = $props();

  const amount = $derived(scaling.amountFor(line));
</script>

<li
  id="ingredient-row-{line.ids[0]}"
  class="row"
  class:highlighted
  onmouseenter={() => onhover?.(line.ids)}
  onmouseleave={() => onhover?.(null)}
>
  <span class="amount">{amount.text}</span>

  <span class="name">
    {line.name}{#if line.note}<span class="note">, {line.note}</span>{/if}
  </span>
</li>

<style>
  .row {
    position: relative;
    isolation: isolate;
    display: grid;
    grid-column: 1 / -1;
    grid-template-columns: subgrid;
    padding-block: var(--space-2);
  }

  /*
   * Lit, not boxed: the highlight is its own layer, animating opacity and transform only, so no
   * layout moves.
   * The way in is quick (set on `.highlighted`); the way out is a plain slow `ease`, as the token
   * curve is too front-loaded.
   */
  .row::before {
    content: '';
    position: absolute;
    z-index: -1;
    inset-block: 0;
    inset-inline: calc(-1 * var(--space-3));
    border-radius: var(--radius-sm);
    background: var(--surface-highlight-band);
    opacity: 0;
    transition: opacity var(--duration-slow) ease;
  }

  /*
   * An accent mark down the edge: a tint alone is not reliable, since the accent is the colour the
   * theme contract proves carries against the page.
   */
  .row::after {
    content: '';
    position: absolute;
    inset-block: var(--space-2);
    inset-inline-start: calc(-1 * var(--space-3) + 3px);
    width: 3px;
    border-radius: var(--radius-full);
    background: var(--accent);
    opacity: 0;
    transform: scaleY(0.4);
    transition:
      opacity var(--duration-slow) ease,
      transform var(--duration-slow) ease;
  }

  .highlighted::before {
    opacity: 1;
    transition-duration: var(--duration-fast);
    transition-timing-function: var(--ease-out);
  }

  .highlighted::after {
    opacity: 1;
    transform: none;
    transition-duration: var(--duration-fast), var(--duration-base);
    transition-timing-function: var(--ease-out);
  }

  .amount {
    font-variant-numeric: tabular-nums;
    font-weight: var(--weight-medium);
    /*
     * Amount and unit are already joined by a non-breaking space; this stops the column wrapping at
     * all.
     */
    white-space: nowrap;
  }

  @media print {
    .row {
      padding-block: 1mm;
    }

    .row::before,
    .row::after {
      display: none;
    }
  }

  .note {
    color: var(--text-muted);
  }
</style>
