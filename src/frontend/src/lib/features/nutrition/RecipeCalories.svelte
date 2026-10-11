<script lang="ts">
  import { VisuallyHidden } from '$ds';
  import type { RecipeSummary } from '$features/recipes/types';
  import { m } from '$shell/i18n';

  import CalorieInfo from './CalorieInfo.svelte';
  import { labelNumber, nbsp } from './format';
  import { perWords } from './headline';

  let { recipe }: { recipe: RecipeSummary } = $props();
  const basis = $derived(
    perWords({
      per: recipe.yieldKind === 'pieces' ? 'piece' : 'serving',
      yield: recipe.yieldAmount
    })
  );
</script>

{#if recipe.calories}
  <span class="calories">
    <span>
      {#if recipe.calories.atLeast}<VisuallyHidden>{m['nutrition.atLeast']()}{nbsp}</VisuallyHidden
        >{/if}
      {labelNumber('energy', recipe.calories)}{nbsp}kcal
    </span>
    <CalorieInfo atLeast={recipe.calories.atLeast} estimated={recipe.calories.estimated} {basis} />
  </span>
{/if}

<style>
  .calories {
    display: inline-flex;
    align-items: center;
    gap: 0.125rem;
    color: inherit;
    font-size: var(--text-sm);
    white-space: nowrap;
    font-variant-numeric: tabular-nums;
  }
</style>
