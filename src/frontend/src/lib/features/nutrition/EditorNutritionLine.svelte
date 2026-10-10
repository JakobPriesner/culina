<script lang="ts">
  import type { Recipe } from '$features/recipes/types';
  import { m } from '$shell/i18n';

  import { coverageWords } from './headline';
  import { nutrition } from './stores/nutrition.svelte';

  /**
   * One quiet line under the ingredients heading of a saved recipe: how many lines count for the nutrition
   * and which do not, as the last save has them. It asks nothing, blocks nothing and is absent while there is
   * no answer (loading, failed, nothing written yet), so the editor never shows a placeholder for it.
   */
  interface Props {
    /** The recipe on screen: the version names what was saved, the ingredients name the lines. */
    recipe: Recipe;
    /** Typing the server has not seen yet: the line says it describes the earlier version. */
    unsaved: boolean;
  }

  let { recipe, unsaved }: Props = $props();

  const answer = $derived(nutrition.answerFor(recipe.id, recipe.householdId));
  const words = $derived(answer ? coverageWords(answer, recipe) : null);

  // A save changes the version, and with it what counts.
  $effect(() => {
    void recipe.version;
    void nutrition.load(recipe.id, recipe.householdId);
  });
</script>

{#if words}
  <p class="coverage">
    {words}
    {#if unsaved}<span class="stale">({m['nutrition.coverage.stale']()})</span>{/if}
  </p>
{/if}

<style>
  .coverage {
    max-width: var(--measure);
    margin-bottom: var(--space-3);
    color: var(--text-muted);
    font-size: var(--text-sm);
    line-height: var(--leading-normal);
    overflow-wrap: anywhere;
  }

  .stale {
    color: var(--text-subtle);
  }
</style>
