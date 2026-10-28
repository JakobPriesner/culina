<script lang="ts">
  import type { RecipeSummary } from '$features/recipes/types';
  import { m } from '$shell/i18n';

  import CalorieInfo from './CalorieInfo.svelte';
  import Qualifier from './Qualifier.svelte';
  import { labelNumber, nbsp } from './format';
  import { perWords } from './headline';

  interface Props {
    recipe: RecipeSummary;
    /**
     * Offers the ⓘ that says what "mind." and "ca." mean. A grid of cards leaves it out: thirty identical buttons
     * (and tab stops) said it thirty times. The featured card above the grid and the recipe's own page say it once.
     */
    explained?: boolean;
  }

  let { recipe, explained = true }: Props = $props();
  const basis = $derived(
    perWords({
      per: recipe.yieldKind === 'pieces' ? 'piece' : 'serving',
      yield: recipe.yieldAmount
    })
  );
  /**
   * Said after the figure, so a card that leaves the yield out still says what it counts. A recipe that makes one serving is a whole
   * cake or a whole pot, and its figure is for all of it: "per person" there would be a very wrong comparison.
   */
  const per = $derived(
    recipe.yieldKind === 'pieces'
      ? m['nutrition.perPiece']()
      : recipe.yieldAmount === 1
        ? m['nutrition.short.whole']()
        : m['nutrition.perServing']()
  );
</script>

{#if recipe.calories?.mostlyMissing}
  <!-- No number at all: "mind. 139 kcal" for a dish whose beef was not counted still reads as a light one. -->
  <span class="calories partial">
    <span>{m['nutrition.calories.mostlyMissing']()}</span>
    {#if explained}<CalorieInfo atLeast mostlyMissing {basis} />{/if}
  </span>
{:else if recipe.calories}
  <span class="calories" class:partial={recipe.calories.atLeast}>
    <span>
      <!-- A lower bound is shown as one, not only said: a small number that is only part of the dish must not read as a light one. -->
      <Qualifier
        atLeast={recipe.calories.atLeast}
        estimated={recipe.calories.estimated}
      />{labelNumber('energy', recipe.calories)}{nbsp}kcal <span class="per">{per}</span>
    </span>
    {#if explained}
      <CalorieInfo
        atLeast={recipe.calories.atLeast}
        estimated={recipe.calories.estimated}
        {basis}
      />
    {/if}
  </span>
{/if}

<style>
  .partial {
    color: var(--text-muted);
  }

  .calories {
    display: inline-flex;
    align-items: center;
    gap: 0.125rem;
    color: inherit;
    font-size: var(--text-sm);
    font-variant-numeric: tabular-nums;
  }

  /* A narrow card wraps between the figure and what it is for, never inside either ("pro Portion" stays one). */
  .per {
    white-space: nowrap;
  }
</style>
