<script lang="ts">
  import { m } from '$shell/i18n';

  import type { RecipeQuery, RecipeSort } from '../stores/libraryView.svelte';
  import { sortLabel, timeLabel } from './labels';

  interface Props {
    view: RecipeQuery;
    /** Tag display names by slug. */
    named: ReadonlyMap<string, string>;
    order: RecipeSort;
  }

  let { view, named, order }: Props = $props();
</script>

{#if view.tags.length > 0 || view.maxMinutes !== null || view.sort !== null}
  <ul class="applied" aria-label={m['filters.applied']()}>
    {#each view.tags as slug (slug)}
      <li>
        <button
          type="button"
          class="chip"
          aria-label={m['filters.chip.remove']({ name: named.get(slug) ?? slug })}
          onclick={() => view.toggleTag(slug)}
        >
          {named.get(slug) ?? slug}
          <span aria-hidden="true">×</span>
        </button>
      </li>
    {/each}

    {#if view.maxMinutes !== null}
      <li>
        <button
          type="button"
          class="chip"
          aria-label={m['filters.chip.remove']({ name: timeLabel(view.maxMinutes) })}
          onclick={() => (view.maxMinutes = null)}
        >
          {timeLabel(view.maxMinutes)}
          <span aria-hidden="true">×</span>
        </button>
      </li>
    {/if}

    {#if view.sort !== null}
      <li>
        <button
          type="button"
          class="chip"
          aria-label={m['filters.chip.remove']({ name: sortLabel(order) })}
          onclick={() => (view.sort = null)}
        >
          {m['filters.chip.sort']({ name: sortLabel(order) })}
          <span aria-hidden="true">×</span>
        </button>
      </li>
    {/if}
  </ul>
{/if}

<style>
  .applied {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2);
    margin: 0;
    padding: 0;
    list-style: none;
  }

  .chip {
    display: inline-flex;
    align-items: center;
    gap: var(--space-2);
    min-height: var(--control-sm);
    padding: 0 var(--space-3);
    border: 1px solid var(--border);
    border-radius: var(--radius-full);
    background: var(--surface-sunken);
    color: var(--text);
    font: inherit;
    font-size: var(--text-sm);
    cursor: pointer;
  }

  .chip:hover {
    border-color: var(--border-strong);
  }
</style>
