<script lang="ts">
  import type { Snippet } from 'svelte';

  import type { Section } from './sections';
  import ShoppingItemRow from './ShoppingItemRow.svelte';
  import type { ShoppingItem } from './stores/shopping.svelte';

  interface Props {
    label: string;
    /** How many lines are in this part of the shop, when that is worth saying. */
    count?: number;
    /** Kept at the bottom and set apart, which is where what is bought goes. */
    apart?: boolean;
    items: readonly ShoppingItem[];
    /** Sits at the end of the heading, next to what it acts on. */
    action?: Snippet;
    oncheck: (item: ShoppingItem, checked: boolean) => void;
    onremove: (item: ShoppingItem) => void;
    onmove: (item: ShoppingItem, section: Section) => void;
  }

  let { label, count, apart = false, items, action, oncheck, onremove, onmove }: Props = $props();
</script>

<section class="section" class:bought={apart}>
  <!-- Stuck to the top of the screen while its own lines scroll past.

       A shopping list is walked, not read: the label is what says which
       part of the shop the next six lines are in, and a label that has
       scrolled off is a list of words with no aisle attached. The offset
       is the one the app's own floating header already claims. -->
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

  /*
   * Opaque, because lines pass underneath it.
   *
   * Bled out to the page's gutter and back so that a row travelling under the
   * heading disappears at the edge of the screen rather than at the edge of the
   * text — and so the rule under it reaches the same edges the rows do.
   */
  .sticky {
    position: sticky;
    top: var(--space-24);
    /* Above the lines it covers and below the app's own header, which one
       section's heading passes under as the next one pushes it up. Sharing the
       header's layer put an aisle name across the navigation. */
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

  /* How many lines are in this part of the shop, which is how you know whether
     to expect the aisle to take a minute or five. */
  .count {
    color: var(--text-subtle);
    font-size: var(--text-xs);
    font-variant-numeric: tabular-nums;
  }

  /* Kept at the bottom and set apart rather than dimmed as a block: opacity on
     the whole section takes its own button's contrast down with it. The rows
     say what they are on their own — struck through, and quieter. */
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
