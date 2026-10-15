<script lang="ts">
  import { VisuallyHidden } from '$ds';
  import type { RecipeSummary } from '$features/recipes/types';
  import { m } from '$shell/i18n';

  import CalorieInfo from './CalorieInfo.svelte';
  import EstimateMark from './EstimateMark.svelte';
  import { labelNumber, nbsp } from './format';
  import { perWords } from './headline';

  let { recipe }: { recipe: RecipeSummary } = $props();
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
        : m['nutrition.short.perPerson']()
  );
</script>

{#if recipe.calories?.mostlyMissing}
  <!-- No number at all: "≥ 139 kcal" for a dish whose beef was not counted still reads as a light one. -->
  <span class="calories partial">
    <span>{m['nutrition.calories.mostlyMissing']()}</span>
    <CalorieInfo atLeast mostlyMissing {basis} />
  </span>
{:else if recipe.calories}
  <span class="calories" class:partial={recipe.calories.atLeast}>
    <span>
      <!-- A lower bound is shown as one, not only said: a small number that is only part of the dish must not read as a light one. -->
      {#if recipe.calories.atLeast}<span class="bound" aria-hidden="true">≥{nbsp}</span
        ><VisuallyHidden>{m['nutrition.atLeast']()}{nbsp}</VisuallyHidden>{/if}
      <EstimateMark estimated={recipe.calories.estimated} />{labelNumber(
        'energy',
        recipe.calories
      )}{nbsp}kcal <span class="per">{per}</span>
    </span>
    <CalorieInfo atLeast={recipe.calories.atLeast} estimated={recipe.calories.estimated} {basis} />
  </span>
{/if}

<style>
  .partial {
    color: var(--text-muted);
  }

  .bound {
    font-weight: var(--weight-regular);
  }

  .calories {
    display: inline-flex;
    align-items: center;
    gap: 0.125rem;
    color: inherit;
    font-size: var(--text-sm);
    font-variant-numeric: tabular-nums;
  }

  /* A narrow card wraps between the figure and what it is for, never inside either ("p. P." stays one). */
  .per {
    white-space: nowrap;
  }
</style>
