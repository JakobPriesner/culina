<script lang="ts">
  import { Button } from '$ds';
  import type { RecipeReading } from '$features/recipes/types';
  import { m } from '$shell/i18n';
  import { toaster } from '$shell/toaster.svelte';
  import { cooking } from './stores/cooking.svelte';
  import { kitchenMediaSession as remote } from './mediaSession.svelte';

  let { recipe = null }: { recipe?: RecipeReading | null } = $props();
  async function toggle() {
    if (remote.active) {
      remote.stop();
      return;
    }
    if (recipe && !(await remote.start(recipe)))
      toaster.show({ message: () => m['cooking.remote.failed'](), tone: 'danger' });
  }
</script>

{#if remote.supported && (recipe || remote.active)}
  <div class="remote">
    <Button
      size="sm"
      loading={remote.starting}
      disabled={!remote.active && cooking.session?.recipeId !== recipe?.id}
      onclick={toggle}
    >
      {remote.active ? m['cooking.remote.disable']() : m['cooking.remote.enable']()}
    </Button>
    {#if recipe}
      <span class="hint">{m['cooking.remote.hint']()}</span>
    {/if}
  </div>
{/if}

<style>
  .remote {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-2);
  }
  .hint {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }
</style>
