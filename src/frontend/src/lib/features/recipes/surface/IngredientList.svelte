<script lang="ts">
  import IngredientRow from './IngredientRow.svelte';
  import type { Scaling } from './scaled.svelte';
  import { combineIngredients } from '../ingredientLines';
  import type { Ingredient } from '../types';

  /**
   * A list of ingredients, added up here rather than by the caller: a step calling for butter twice needs one piece.
   * No heading and no recipe groups: named groups are said by the per-step arrangement beside the step, and saying it twice goes stale after edits.
   */
  interface Props {
    ingredients: readonly Ingredient[];
    /** The one source of every amount on the surface. */
    scaling: Scaling;
    /** Which ingredient the reader is pointing at, from a step's text. */
    highlighted?: string | null;
    /** Forwarded when an ingredient row is hovered, for bidirectional highlighting */
    onhover?: (ingredientId: string | null) => void;
  }

  let { ingredients, scaling, highlighted = null, onhover }: Props = $props();

  const lines = $derived(combineIngredients(ingredients));
</script>

<ul class="list">
  {#each lines as line (line.ids[0])}
    <IngredientRow
      {line}
      {scaling}
      highlighted={highlighted !== null && line.ids.includes(highlighted)}
      onhover={(ids) => onhover?.(ids && ids.length > 0 ? (ids[0] ?? null) : null)}
    />
  {/each}
</ul>

<style>
  /* One grid for the whole list so the widest amount sets the column and names line up (per-row columns left them ragged), with a floor for short amounts. */
  .list {
    display: grid;
    grid-template-columns: minmax(5rem, max-content) minmax(0, 1fr);
    column-gap: var(--space-4);
    margin: 0;
    padding: 0;
    list-style: none;
  }

  /* Amount column sized for a thumb on screen; on paper the halves read better close. */
  @media print {
    .list {
      grid-template-columns: minmax(3.75rem, max-content) minmax(0, 1fr);
      column-gap: var(--space-2);
    }
  }
</style>
