<script lang="ts">
  import IngredientRow from './IngredientRow.svelte';
  import type { Scaling } from './scaled.svelte';
  import { combineIngredients } from '../ingredientLines';
  import type { Ingredient } from '../types';

  /**
   * A list of ingredients, added up.
   *
   * Adding up is the list's own job rather than the caller's, because every
   * list on this surface wants it: the panel adds up the whole recipe, and a
   * step that calls for butter twice is a step that needs one piece of butter.
   *
   * There is no heading inside it, and a recipe's ingredient groups are not
   * rendered as one. A named group is the recipe saying "these five belong to
   * the béchamel", and the per-step arrangement says the same thing in the
   * place where it is actually useful — beside the step that makes the
   * béchamel. Two ways of saying it is one too many, and only one of them is
   * ever accurate for a recipe somebody has since edited.
   */
  interface Props {
    ingredients: readonly Ingredient[];
    /** The one source of every amount on the surface. */
    scaling: Scaling;
    /** Which ingredient the reader is pointing at, from a step's text. */
    highlighted?: string | null;
  }

  let { ingredients, scaling, highlighted = null }: Props = $props();

  const lines = $derived(combineIngredients(ingredients));
</script>

<ul class="list">
  {#each lines as line (line.ids[0])}
    <IngredientRow
      {line}
      {scaling}
      highlighted={highlighted !== null && line.ids.includes(highlighted)}
    />
  {/each}
</ul>

<style>
  /*
   * One grid for the whole list, not one per row.
   *
   * A row that sizes its own amount column leaves every name starting
   * somewhere different — "200 g Zwiebel" and "200 Milliliter Rotwein" push
   * their names three characters apart — and the list reads as a ragged pile
   * rather than a table. Here the widest amount sets the column and every name
   * below it lines up, with a floor so that a list of short amounts still
   * leaves the names clear of them.
   */
  .list {
    display: grid;
    grid-template-columns: minmax(5rem, max-content) minmax(0, 1fr);
    column-gap: var(--space-4);
    margin: 0;
    padding: 0;
    list-style: none;
  }

  /* The amount column is sized for a thumb to land beside on a screen. On
     paper the eye does the work, and the two halves read better close. */
  @media print {
    .list {
      grid-template-columns: minmax(3.75rem, max-content) minmax(0, 1fr);
      column-gap: var(--space-2);
    }
  }
</style>
