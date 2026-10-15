<script lang="ts">
  import type { Snippet } from 'svelte';

  import type { Section } from './sections';
  import ShoppingItemRow from './ShoppingItemRow.svelte';
  import { endsItsName } from './lineRuns';
  import type { ShoppingItem } from './stores/shopping.svelte';

  interface Props {
    label: string;
    count?: number;
    apart?: boolean;
    items: readonly ShoppingItem[];
    action?: Snippet;
    oncheck: (item: ShoppingItem, checked: boolean) => void;
    onremove: (item: ShoppingItem) => void;
    onmove: (item: ShoppingItem, section: Section) => void;
  }

  let { label, count, apart = false, items, action, oncheck, onremove, onmove }: Props = $props();
</script>

{#snippet rows()}
  <ul class="list">
    {#each items as item, index (item.itemId)}
      <!-- Lines of one thing that could not be added up share one note, under the last of them. -->
      <ShoppingItemRow
        {item}
        noted={endsItsName(items, index)}
        oncheck={(isChecked) => oncheck(item, isChecked)}
        onremove={() => onremove(item)}
        onmove={(section) => onmove(item, section)}
      />
    {/each}
  </ul>
{/snippet}

{#if apart}
  <details class="section bought">
    <summary class="heading-row">
      <svg
        class="chevron"
        aria-hidden="true"
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        stroke-width="2"
        ><path d="m9 6 6 6-6 6" stroke-linecap="round" stroke-linejoin="round" /></svg
      >
      <span class="label">{label}</span>
      {#if count !== undefined}<span class="count">{count}</span>{/if}
    </summary>
    {@render rows()}
    {#if action}<div class="completion-actions">{@render action()}</div>{/if}
  </details>
{:else}
  <section class="section">
    <h2 class="heading-row sticky">
      <span class="label">{label}</span>
      {#if count !== undefined}<span class="count">{count}</span>{/if}
      {@render action?.()}
    </h2>
    {@render rows()}
  </section>
{/if}

<style>
  .section {
    margin-bottom: var(--space-6);
  }
  .heading-row {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-2);
    margin-bottom: var(--space-1);
  }
  /* Over the header's fade, which starts where it sticks: under it, a stuck heading read washed out. */
  .sticky {
    position: sticky;
    top: var(--header-inset);
    z-index: var(--z-shell);
    padding-block: var(--space-2);
    background: var(--surface);
  }
  .label {
    font-size: var(--text-xs);
    font-weight: var(--weight-semibold);
    color: var(--accent);
  }
  .count {
    color: var(--text-muted);
    font-size: var(--text-xs);
    font-variant-numeric: tabular-nums;
  }
  .bought {
    margin-top: var(--space-6);
  }
  .bought .label {
    color: var(--text-muted);
  }
  summary {
    min-height: var(--control-sm);
    cursor: pointer;
    list-style: none;
  }
  summary::-webkit-details-marker {
    display: none;
  }
  .chevron {
    width: var(--space-4);
    height: var(--space-4);
    color: var(--text-muted);
    transition: transform var(--duration-fast) var(--ease-out);
  }
  details[open] .chevron {
    transform: rotate(90deg);
  }
  .completion-actions {
    margin-top: var(--space-3);
  }
  .list {
    margin: 0;
    padding: 0;
    list-style: none;
  }
</style>
