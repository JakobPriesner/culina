<script lang="ts">
  import { Button, VisuallyHidden } from '$ds';
  import { explain } from '$shell/explain';
  import { m } from '$shell/i18n';
  import { toaster } from '$shell/toaster.svelte';

  import { ingredientNames as store } from './stores/ingredientNames.svelte';
  import type { IngredientMerge } from './types';

  /**
   * The merges a household made: names saved as another whenever a recipe is written. Removing one stops that and
   * renames nothing back. The page loads the store; the names panel says when it could not.
   */
  interface Props {
    householdId: string;
  }

  let { householdId }: Props = $props();

  const status = $derived(store.status(householdId));
  const merges = $derived(store.merges(householdId));

  async function remove(merge: IngredientMerge) {
    const failure = await store.removeMerge(householdId, merge.from);

    toaster.show(
      failure
        ? { message: () => explain(failure), tone: 'danger' }
        : { message: () => m['ingredients.merges.removed']({ from: merge.from }), tone: 'success' }
    );
  }
</script>

{#if status === 'ready'}
  {#if merges.length === 0}
    <p class="quiet">{m['ingredients.merges.empty']()}</p>
  {:else}
    <ul class="rows" aria-label={m['ingredients.merges.title']()}>
      {#each merges as merge (merge.from)}
        <li class="row">
          <p class="pair">
            <span class="from">{merge.from}</span>
            <span aria-hidden="true">→</span>
            <VisuallyHidden>{m['ingredients.into']()}</VisuallyHidden>
            <span class="to">{merge.to}</span>
          </p>

          <Button
            size="sm"
            variant="ghost"
            label={m['ingredients.merges.removeLabel']({ from: merge.from, to: merge.to })}
            onclick={() => void remove(merge)}
          >
            {m['ingredients.merges.remove']()}
          </Button>
        </li>
      {/each}
    </ul>
  {/if}
{/if}

<style>
  .quiet {
    max-width: var(--measure);
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .rows {
    max-width: var(--measure);
    margin: 0;
    padding: 0;
    list-style: none;
  }

  .row {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-2) var(--space-4);
    min-height: var(--control-md);
    padding-block: var(--space-2);
    border-bottom: 1px solid var(--border);
  }

  .pair {
    min-width: 0;
    overflow-wrap: anywhere;
  }

  .from {
    color: var(--text-muted);
    text-decoration: line-through;
  }

  .to {
    font-weight: var(--weight-semibold);
  }
</style>
