<script lang="ts" module>
  /** The sheet that is up over the recipe, if any. */
  export type RecipeSheet = 'plan' | 'cookbook' | 'share';
</script>

<script lang="ts">
  import AddToCookbookSheet from '$features/cookbooks/AddToCookbookSheet.svelte';
  import PlanRecipeSheet from '$features/planning/PlanRecipeSheet.svelte';
  import DeleteRecipeDialog from '$features/recipes/DeleteRecipeDialog.svelte';
  import ShareRecipeSheet from '$features/recipes/ShareRecipeSheet.svelte';
  import type { useRecipeDeletion } from './useRecipeDeletion.svelte';

  interface Props {
    recipeId: string;
    title: string;
    householdId: string | null;
    servings: number;
    open: RecipeSheet | null;
    deletion: ReturnType<typeof useRecipeDeletion>;
  }

  let { recipeId, title, householdId, servings, open = $bindable(), deletion }: Props = $props();

  const close = () => (open = null);
</script>

<DeleteRecipeDialog
  open={deletion.ui.doomed !== null}
  title={deletion.ui.doomed?.title ?? ''}
  deleting={deletion.ui.deleting}
  error={deletion.ui.failure}
  onconfirm={() => void deletion.confirm()}
  onclose={deletion.cancel}
/>

<ShareRecipeSheet open={open === 'share'} {recipeId} {title} onclose={close} />

{#if householdId}
  <PlanRecipeSheet
    open={open === 'plan'}
    {householdId}
    {recipeId}
    {title}
    {servings}
    onclose={close}
  />

  <AddToCookbookSheet open={open === 'cookbook'} {householdId} {recipeId} onclose={close} />
{/if}
