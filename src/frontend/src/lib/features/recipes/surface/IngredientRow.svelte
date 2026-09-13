<script lang="ts">
  import type { Scaling } from './scaled.svelte';
  import type { Ingredient } from '../types';

  /**
   * One line of the ingredient list.
   *
   * The amount is its own element so it can be highlighted when it changes —
   * the number morphs in place rather than the row being replaced, which is
   * what lets the eye see *what* changed when the servings move.
   */
  interface Props {
    ingredient: Ingredient;
    scaling: Scaling;
    /** Lit while the step that uses it is being read. */
    highlighted?: boolean;
  }

  let { ingredient, scaling, highlighted = false }: Props = $props();

  const amount = $derived(scaling.amountFor(ingredient));
</script>

<li class="row" class:highlighted data-ingredient={ingredient.id}>
  <span class="amount">{amount.text}</span>

  <span class="name">
    {ingredient.name}{#if ingredient.note}<span class="note">, {ingredient.note}</span>{/if}
  </span>
</li>

<style>
  .row {
    display: grid;
    grid-template-columns: minmax(5rem, auto) 1fr;
    gap: var(--space-4);
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

  .note {
    color: var(--text-muted);
  }

  /* The amount column is sized for a thumb to land beside on a screen. On
     paper the eye does the work, and the two halves read better close. */
  @media print {
    .row {
      /* A floor, not `auto`: each row is its own grid, so a column that sizes
         to its content leaves every amount a different width and the names
         ragged down the page. Narrower than the screen's, which is sized for a
         thumb to land beside. */
      grid-template-columns: minmax(3.75rem, auto) 1fr;
      gap: var(--space-2);
      padding-block: 1mm;
    }

    .highlighted {
      background: none;
      box-shadow: none;
    }
  }
</style>
