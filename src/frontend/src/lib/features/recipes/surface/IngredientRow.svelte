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
  }

  let { line, scaling, highlighted = false }: Props = $props();

  const amount = $derived(scaling.amountFor(line));
</script>

<li class="row" class:highlighted>
  <span class="amount">{amount.text}</span>

  <span class="name">
    {line.name}{#if line.note}<span class="note">, {line.note}</span>{/if}
  </span>
</li>

<style>
  .row {
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
    background: var(--surface-accent-subtle);
    box-shadow: 0 0 0 var(--space-2) var(--surface-accent-subtle);
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
  }

  .note {
    color: var(--text-muted);
  }
</style>
