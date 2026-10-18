<script lang="ts" module>
  /** The sheet that is up over the recipe, if any. */
  export type RecipeSheet = 'plan' | 'cookbook' | 'share' | 'list';
</script>

<script lang="ts">
  import { untrack } from 'svelte';

  import type { Recipe } from '../types';
  import type { useRecipeDeletion } from './useRecipeDeletion.svelte';

  /**
   * Each sheet, and the delete dialog, is downloaded the first time it opens, not with the recipe, which is the heaviest cold load in the app;
   * once there it stays mounted so it can animate closed and open again at once.
   */
  interface Props {
    recipeId: string;
    title: string;
    householdId: string | null;
    servings: number;
    /** The recipe itself, once it has arrived: the list sheet shows its ingredients. */
    recipe: Recipe | null;
    open: RecipeSheet | null;
    deletion: ReturnType<typeof useRecipeDeletion>;
  }

  let {
    recipeId,
    title,
    householdId,
    servings,
    recipe,
    open = $bindable(),
    deletion
  }: Props = $props();

  let opened = $state<readonly RecipeSheet[]>([]);
  let askedToDelete = $state(false);

  $effect.pre(() => {
    const wanted = open;
    const known = untrack(() => opened);

    if (wanted && !known.includes(wanted)) {
      opened = [...known, wanted];
    }
  });

  $effect.pre(() => {
    if (deletion.ui.doomed) {
      askedToDelete = true;
    }
  });

  const close = () => (open = null);
</script>

{#if askedToDelete}
  {#await import('$features/recipes/DeleteRecipeDialog.svelte') then { default: DeleteRecipeDialog }}
    <DeleteRecipeDialog
      open={deletion.ui.doomed !== null}
      title={deletion.ui.doomed?.title ?? ''}
      deleting={deletion.ui.deleting}
      error={deletion.ui.failure}
      onconfirm={() => void deletion.confirm()}
      onclose={deletion.cancel}
    />
  {/await}
{/if}

{#if opened.includes('share')}
  {#await import('$features/recipes/ShareRecipeSheet.svelte') then { default: ShareRecipeSheet }}
    <ShareRecipeSheet open={open === 'share'} {recipeId} {title} onclose={close} />
  {/await}
{/if}

{#if householdId}
  {#if opened.includes('plan')}
    {#await import('$features/planning/PlanRecipeSheet.svelte') then { default: PlanRecipeSheet }}
      <PlanRecipeSheet
        open={open === 'plan'}
        {householdId}
        {recipeId}
        {title}
        {servings}
        onclose={close}
      />
    {/await}
  {/if}

  {#if opened.includes('list') && recipe}
    {#await import('./AddToListSheet.svelte') then { default: AddToListSheet }}
      <AddToListSheet open={open === 'list'} {householdId} {recipe} {servings} onclose={close} />
    {/await}
  {/if}

  {#if opened.includes('cookbook')}
    {#await import('$features/cookbooks/AddToCookbookSheet.svelte') then { default: AddToCookbookSheet }}
      <AddToCookbookSheet open={open === 'cookbook'} {householdId} {recipeId} onclose={close} />
    {/await}
  {/if}
{/if}
