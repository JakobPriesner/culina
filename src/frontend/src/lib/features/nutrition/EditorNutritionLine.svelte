<script lang="ts">
  import type { Recipe } from '$features/recipes/types';
  import { formatNumber, m } from '$shell/i18n';

  import { shortReason } from './headline';
  import { createLineCorrection } from './lineCorrection.svelte';
  import LineCorrectionSheet from './LineCorrectionSheet.svelte';
  import { nutrition } from './stores/nutrition.svelte';

  /**
   * One quiet line under the ingredients heading of a saved recipe: how many lines count for the nutrition
   * and which do not, as the last save has them. Each line that does not count is a button to say what it
   * is (or what one unit weighs) right here, the same sheet the recipe page opens. Absent while there is no
   * answer (loading, failed, nothing written yet), so the editor never shows a placeholder for it.
   */
  interface Props {
    /** The recipe on screen: the version names what was saved, the ingredients name the lines. */
    recipe: Recipe;
    /** Typing the server has not seen yet: the line says it describes the earlier version. */
    unsaved: boolean;
  }

  let { recipe, unsaved }: Props = $props();

  const answer = $derived(nutrition.answerFor(recipe.id, recipe.householdId));

  const correction = createLineCorrection({
    recipe: () => recipe,
    nutrition: () => answer,
    householdId: () => recipe.householdId
  });

  /** At most three named, in the recipe's order, then how many more. */
  const left = $derived(correction.rows.filter(({ line }) => line.status !== 'counted'));
  const named = $derived(left.slice(0, 3));

  /** Spaced in an expression: Svelte drops the whitespace at a block's edge, which glued "·" to the word before. */
  const dot = ' · ';

  // A save changes the version, and with it what counts.
  $effect(() => {
    void recipe.version;
    void nutrition.load(recipe.id, recipe.householdId);
  });
</script>

{#if answer && answer.lines > 0}
  <p class="coverage">
    {#if answer.counted === answer.lines}
      {m['nutrition.coverage.all']()}
    {:else}
      {m['nutrition.coverage.some']({
        counted: formatNumber(answer.counted),
        lines: formatNumber(answer.lines)
      })}
      {#each named as { ingredient, line } (ingredient.id)}
        <span aria-hidden="true">{dot}</span>
        <button
          type="button"
          class="fix"
          aria-label={m['nutrition.change']({ name: ingredient.name })}
          onclick={() => correction.edit(ingredient.id, line.status === 'amountNotInGrams')}
          >{m['nutrition.coverage.line']({ name: ingredient.name, why: shortReason(line) })}</button
        >
      {/each}
      {#if left.length > named.length}
        <span aria-hidden="true">{dot}</span>
        {m['nutrition.coverage.more']({ count: formatNumber(left.length - named.length) })}
      {/if}
    {/if}
    {#if unsaved}<span class="stale">({m['nutrition.coverage.stale']()})</span>{/if}
  </p>

  <LineCorrectionSheet {correction} householdId={recipe.householdId} />
{/if}

<style>
  .coverage {
    max-width: var(--measure);
    margin-bottom: var(--space-3);
    color: var(--text-muted);
    font-size: var(--text-sm);
    line-height: var(--leading-normal);
  }

  /* A line of the sentence that can be answered: underlined like a link, the words unchanged. */
  .fix {
    padding: 0;
    border: 0;
    background: none;
    color: inherit;
    font: inherit;
    text-align: start;
    text-decoration: underline dotted;
    text-underline-offset: 0.2em;
    cursor: pointer;
  }

  @media (hover: hover) {
    .fix:hover {
      color: var(--text);
    }
  }

  .stale {
    color: var(--text-subtle);
  }
</style>
