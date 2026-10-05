<script lang="ts">
  import { goto } from '$app/navigation';
  import { resolve } from '$app/paths';
  import { Button } from '$ds';
  import type { RecipeReading } from '$features/recipes/types';
  import { m } from '$shell/i18n';
  import { toaster } from '$shell/toaster.svelte';
  import { cooking } from './stores/cooking.svelte';
  import { cookingPip } from './pip.svelte';

  let { recipe }: { recipe: RecipeReading } = $props();
  async function toggle() {
    if (cookingPip.active) {
      cookingPip.close();
      return;
    }
    const id = recipe.id;
    const opened = await cookingPip.open(recipe, () => {
      void goto(
        // eslint-disable-next-line svelte/no-navigation-without-resolve -- The route is resolved before its yield is appended.
        `${resolve('/(app)/recipes/[recipeId]/cook', { recipeId: id })}?yield=${cooking.session?.servings ?? recipe.yieldAmount}`
      );
    });
    if (!opened) toaster.show({ message: () => m['cooking.pip.failed'](), tone: 'danger' });
  }
</script>

{#if cookingPip.supported}
  <Button
    size="sm"
    disabled={cooking.session?.recipeId !== recipe.id}
    loading={cookingPip.opening}
    onclick={toggle}
  >
    {#snippet icon()}
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
        <rect x="3" y="4" width="18" height="16" rx="2" />
        <rect x="12" y="11" width="6" height="6" rx="1" />
      </svg>
    {/snippet}
    {cookingPip.active ? m['cooking.pip.close']() : m['cooking.pip.open']()}
  </Button>
{/if}
