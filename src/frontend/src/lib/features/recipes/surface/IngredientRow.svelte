<script lang="ts">
  import type { Scaling } from './scaled.svelte';
  import type { IngredientLine } from '../ingredientLines';

  /**
   * One line of the ingredient list.
   *
   * The amount is its own element so it can be highlighted when it changes —
   * the number morphs in place rather than the row being replaced, which is
   * what lets the eye see *what* changed when the servings move.
   *
   * Its two columns come from the list around it rather than from the row, so
   * that every name starts at the same place. See `IngredientList`.
   */
  interface Props {
    line: IngredientLine;
    scaling: Scaling;
    /** Lit while the step that uses it is being read. */
    highlighted?: boolean;
    /** Emitted when row is hovered or focused, for bidirectional highlighting */
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
    display: grid;
    grid-column: 1 / -1;
    grid-template-columns: subgrid;
    padding-block: var(--space-2);
    border-radius: var(--radius-sm);
    transition: background-color var(--duration-base) var(--ease-out);
  }

  /* Lit, not boxed: the row keeps its place in the list and the eye is drawn
     to it without the layout moving a pixel. */
  .highlighted {
    background: var(--surface-highlight);
    box-shadow: 0 0 0 var(--space-2) var(--surface-highlight);
  }

  /*
   * A mark down the edge, because a tint alone cannot be relied on here.
   *
   * The list sits on a sunken card, and in a warm palette the distance between
   * a card and a tint on that card is small in light mode however the tint is
   * chosen — the previous one landed at a contrast ratio of 1.03, which is to
   * say nothing visibly happened at all. The accent is the one colour the theme
   * contract already proves carries against the page, so the answer that holds
   * in both modes is a line of it rather than a louder wash.
   *
   * Absolutely placed, so a row that lights up does not move the row below it,
   * and out at the halo's own edge so that the mark reads as the edge of the
   * lit band rather than as something inside the row.
   */
  .highlighted::before {
    content: '';
    position: absolute;
    inset-block: 0;
    inset-inline-start: calc(-1 * var(--space-2));
    width: 2px;
    border-radius: var(--radius-full);
    background: var(--accent);
  }

  .amount {
    font-variant-numeric: tabular-nums;
    font-weight: var(--weight-medium);
    /* Amount and unit already travel joined by a non-breaking space; this
       keeps the column from wrapping at all. */
    white-space: nowrap;
  }

  @media print {
    .row {
      padding-block: 1mm;
    }

    .highlighted {
      background: none;
      box-shadow: none;
    }

    .highlighted::before {
      display: none;
    }
  }

  .note {
    color: var(--text-muted);
  }
</style>
