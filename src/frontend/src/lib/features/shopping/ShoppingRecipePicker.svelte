<script lang="ts">
  import { Button } from '$ds';
  import RecipePicker from '$features/recipes/RecipePicker.svelte';
  import type { RecipeSummary } from '$features/recipes/types';
  import { explain } from '$shell/explain';
  import { m } from '$shell/i18n';
  import { toaster } from '$shell/toaster.svelte';
  import { shopping } from './stores/shopping.svelte';

  interface Props {
    open: boolean;
    householdId: string;
  }

  let { open = $bindable(), householdId }: Props = $props();

  /** Recipes already added during this opening of the picker. */
  let taken = $state<string[]>([]);

  /**
   * Adds a recipe at its own yield (scaling is chosen on the recipe page).
   * The sheet stays open for the next one; the taken row is the receipt, since a toast would sit under the native dialog.
   */
  async function addRecipe(recipe: RecipeSummary) {
    if (taken.includes(recipe.id)) {
      return;
    }

    // Marked before the round trip so the row cannot be pressed twice.
    taken = [...taken, recipe.id];

    const failure = await shopping.addRecipe(householdId, recipe.id, recipe.yieldAmount);

    if (!failure) {
      return;
    }

    // Close first: a toast cannot show above a native dialog.
    taken = taken.filter((id) => id !== recipe.id);
    open = false;

    toaster.show({ message: () => explain(failure), tone: 'danger' });
  }

  function stop() {
    open = false;
    taken = [];
  }
</script>

<RecipePicker
  {open}
  {householdId}
  title={m['shopping.pick.title']()}
  {taken}
  onpick={(recipe) => void addRecipe(recipe)}
  onclose={stop}
>
  {#snippet footer()}
    <Button variant="primary" onclick={stop}>{m['picker.done']()}</Button>
  {/snippet}
</RecipePicker>
