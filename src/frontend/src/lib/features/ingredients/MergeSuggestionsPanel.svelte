<script lang="ts">
  import type { AppError } from '$api';
  import { Button, Skeleton, VisuallyHidden } from '$ds';
  import { explain } from '$shell/explain';
  import { m } from '$shell/i18n';
  import { toaster } from '$shell/toaster.svelte';

  import { reasonWords } from './reasons';
  import { ingredientNames as store } from './stores/ingredientNames.svelte';
  import type { MergeSuggestion } from './types';

  /**
   * Names the server thinks are one ingredient, each with the name it would keep: merge it, or say it is its own
   * ingredient so it is not suggested again. The page loads the store.
   */
  interface Props {
    householdId: string;
    /** After any merge, for what else on the page depends on names (nutrition choices move with them). */
    onmerged?: () => void;
  }

  let { householdId, onmerged }: Props = $props();

  const status = $derived(store.status(householdId));
  const suggestions = $derived(store.suggestions(householdId));

  let mergingAll = $state(false);

  function failed(failure: AppError) {
    toaster.show({ message: () => explain(failure), tone: 'danger' });
  }

  async function merge(suggestion: MergeSuggestion) {
    const result = await store.merge(householdId, suggestion.from.name, suggestion.to.name);

    if (!result.ok) {
      failed(result.error);

      return;
    }

    onmerged?.();
    toaster.show({
      message: () => m['ingredients.merged']({ from: suggestion.from.name, to: result.value }),
      tone: 'success'
    });
  }

  async function mergeAll() {
    mergingAll = true;

    const { merged, error } = await store.mergeAll(householdId);

    mergingAll = false;

    if (merged > 0) {
      onmerged?.();
      toaster.show({
        message: () => m['ingredients.mergedAll']({ count: merged }),
        tone: 'success'
      });
    }

    if (error) {
      failed(error);
    }
  }

  async function keepApart(suggestion: MergeSuggestion) {
    const failure = await store.keepApart(householdId, suggestion.from.name);

    toaster.show(
      failure
        ? { message: () => explain(failure), tone: 'danger' }
        : {
            message: () => m['ingredients.keptApart']({ from: suggestion.from.name }),
            tone: 'success'
          }
    );
  }
</script>

<div class="panel">
  {#if status === 'failed'}
    <div class="quiet" role="status">
      <p>{m['ingredients.failed']()}</p>
      <Button size="sm" onclick={() => void store.load(householdId)}>{m['error.retry']()}</Button>
    </div>
  {:else if status !== 'ready'}
    <div
      class="skeletons"
      role="status"
      aria-busy="true"
      aria-label={m['ingredients.suggestions.loading']()}
    >
      {#each [0, 1] as row (row)}
        <Skeleton width={row % 2 ? '55%' : '75%'} height="1.25rem" />
      {/each}
    </div>
  {:else if suggestions.length === 0}
    <p class="quiet">{m['ingredients.suggestions.empty']()}</p>
  {:else}
    <ul class="rows" aria-label={m['ingredients.suggestions.title']()}>
      {#each suggestions as suggestion (suggestion.from.name)}
        <li class="row">
          <div class="words">
            <p class="pair">
              <span class="from">{suggestion.from.name}</span>
              <span aria-hidden="true">→</span>
              <VisuallyHidden>{m['ingredients.into']()}</VisuallyHidden>
              <span class="to">{suggestion.to.name}</span>
            </p>
            <p class="meta">
              {reasonWords[suggestion.reason]()} · {m['ingredients.recipes']({
                count: suggestion.from.recipeCount
              })} → {m['ingredients.recipes']({ count: suggestion.to.recipeCount })}
            </p>
          </div>

          <span class="actions">
            <Button
              size="sm"
              disabled={mergingAll}
              label={m['ingredients.suggestions.mergeLabel']({
                from: suggestion.from.name,
                to: suggestion.to.name
              })}
              onclick={() => void merge(suggestion)}
            >
              {m['ingredients.suggestions.merge']()}
            </Button>
            <Button
              size="sm"
              variant="ghost"
              disabled={mergingAll}
              label={m['ingredients.suggestions.keepApartLabel']({
                from: suggestion.from.name,
                to: suggestion.to.name
              })}
              onclick={() => void keepApart(suggestion)}
            >
              {m['ingredients.suggestions.keepApart']()}
            </Button>
          </span>
        </li>
      {/each}
    </ul>

    {#if suggestions.length > 1}
      <div class="all">
        <Button size="sm" loading={mergingAll} onclick={() => void mergeAll()}>
          {m['ingredients.suggestions.mergeAll']()}
        </Button>
      </div>
    {/if}
  {/if}
</div>

<style>
  .panel {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
    max-width: var(--measure);
    min-width: 0;
  }

  .quiet {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-2) var(--space-4);
    margin: 0;
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .skeletons {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
  }

  .rows {
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
    padding-block: var(--space-3);
    border-bottom: 1px solid var(--border);
  }

  .words {
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
    min-width: 0;
  }

  .pair {
    overflow-wrap: anywhere;
  }

  .from {
    color: var(--text-muted);
  }

  .to {
    font-weight: var(--weight-semibold);
  }

  .meta {
    color: var(--text-subtle);
    font-size: var(--text-xs);
  }

  .actions {
    display: flex;
    gap: var(--space-2);
  }

  .all {
    display: flex;
    justify-content: flex-end;
  }
</style>
