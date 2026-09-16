<script lang="ts">
  import { Checkbox, IconButton } from '$ds';

  import { formatQuantity } from '$features/recipes/formatQuantity';
  import { quantityLabels } from '$features/recipes/quantityLabels';
  import { scaleQuantity } from '$features/recipes/scaling';
  import { m } from '$shell/i18n';
  import { preferences } from '$shell/preferences.svelte';
  import type { ShoppingItem } from './stores/shopping.svelte';

  /**
   * One line, in a shop, held one-handed.
   *
   * The whole row is the tick: a checkbox alone is a 16px target, and this is
   * read while walking. The amount is rounded here, at the last possible
   * moment — the server stores the exact sum so that adding three recipes does
   * not compound rounding error.
   */
  interface Props {
    item: ShoppingItem;
    oncheck: (isChecked: boolean) => void;
    onremove: () => void;
  }

  let { item, oncheck, onremove }: Props = $props();

  const amount = $derived(
    formatQuantity(
      scaleQuantity({ value: item.quantity ?? null, unit: item.unit ?? null }, 1),
      preferences.locale,
      quantityLabels
    ).text
  );
</script>

<li class="row" class:bought={item.isChecked}>
  <Checkbox
    checked={item.isChecked}
    label={item.name}
    onchange={(isChecked) => oncheck(isChecked)}
  />

  <!-- Beside the name rather than flushed to the far edge. Ranged right across
       a column this wide, "Zucchini" and "3" end up an inch apart with nothing
       between them, and the eye has to travel the gap for every line. Read as
       a phrase — "cherry tomatoes, 250 g" — it is one glance. -->
  {#if amount}
    <span class="amount">{amount}</span>
  {/if}

  <span class="remove">
    <IconButton label={m['shopping.remove']({ name: item.name })} size="sm" onclick={onremove}>
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
        <path d="m6 6 12 12M18 6 6 18" stroke-linecap="round" />
      </svg>
    </IconButton>
  </span>
</li>

<style>
  .row {
    display: grid;
    grid-template-columns: minmax(0, auto) minmax(0, 1fr) auto;
    align-items: center;
    gap: var(--space-3);
    /* Bled out to the gutter and back so the hover lands on the whole line
       rather than on the words: the line is the target, not the text. */
    margin-inline: calc(var(--space-3) * -1);
    padding-inline: var(--space-3);
    border-radius: var(--radius-md);
    transition: background-color var(--duration-fast) var(--ease-out);
  }

  .row:hover {
    background: var(--surface-hover);
  }

  /* Struck through and dimmed rather than removed: seeing what is already in
     the trolley is how you know you have not missed anything. */
  .bought {
    color: var(--text-subtle);
    text-decoration: line-through;
  }

  .amount {
    justify-self: start;
    color: var(--text-muted);
    font-size: var(--text-sm);
    font-variant-numeric: tabular-nums;
    white-space: nowrap;
  }

  .bought .amount {
    color: inherit;
  }

  /* Held back until the line is reached. A column of crosses down the edge of
     a list reads as the thing to press, and it is the one action here that
     cannot be undone. */
  .remove {
    opacity: 0;
    transition: opacity var(--duration-fast) var(--ease-out);
  }

  .row:hover .remove,
  .remove:focus-within {
    opacity: 1;
  }

  /* A finger has no hover, so on touch there is nothing to reveal it with. */
  @media (hover: none) {
    .remove {
      opacity: 1;
    }
  }
</style>
