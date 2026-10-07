<script lang="ts">
  import type { Snippet } from 'svelte';

  import type { Section } from './sections';
  import ShoppingItemRow from './ShoppingItemRow.svelte';
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

<section class="section" class:bought={apart}>
  <!-- Sticky so the aisle label stays visible while its lines scroll. -->
  <h2 class="heading-row sticky">
    <span class="label">{label}</span>
    {#if count !== undefined}<span class="count">{count}</span>{/if}
    {@render action?.()}
  </h2>

  <ul class="list">
    {#each items as item (item.itemId)}
      <ShoppingItemRow
        {item}
        oncheck={(isChecked) => oncheck(item, isChecked)}
        onremove={() => onremove(item)}
        onmove={(section) => onmove(item, section)}
      />
    {/each}
  </ul>
</section>

<style>
  .section {
    margin-bottom: var(--space-8);
  }

  .heading-row {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-3);
    margin-bottom: var(--space-3);
  }

  /* Opaque, since rows pass underneath; bled to the gutter so they vanish at the screen edge. */
  .sticky {
    position: sticky;
    top: var(--space-24);
    /* Above the rows, below the app header; sharing the header's layer put an aisle name over the navigation. */
    z-index: 1;
    margin-inline: calc(var(--layout-gutter) * -1);
    padding: var(--space-2) var(--layout-gutter);
    background: var(--surface);
  }

  .label {
    font-size: var(--text-xs);
    font-weight: var(--weight-semibold);
    letter-spacing: 0.14em;
    text-transform: uppercase;
    color: var(--text-muted);
  }

  .count {
    color: var(--text-subtle);
    font-size: var(--text-xs);
    font-variant-numeric: tabular-nums;
  }

  /* Set apart rather than dimmed: section opacity would also lower its button's contrast. */
  .bought {
    margin-top: var(--space-12);
    padding-top: var(--space-6);
    border-top: 1px solid var(--border);
  }

  .list {
    margin: 0;
    padding: 0;
    list-style: none;
  }
</style>
