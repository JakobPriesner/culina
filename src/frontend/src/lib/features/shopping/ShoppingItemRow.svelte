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

  {#if amount}
    <span class="amount">{amount}</span>
  {/if}

  <IconButton label={m['shopping.remove']({ name: item.name })} size="sm" onclick={onremove}>
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
      <path d="m6 6 12 12M18 6 6 18" stroke-linecap="round" />
    </svg>
  </IconButton>
</li>

<style>
  .row {
    display: grid;
    grid-template-columns: 1fr auto auto;
    align-items: center;
    gap: var(--space-3);
    padding-block: var(--space-1);
  }

  /* Struck through and dimmed rather than removed: seeing what is already in
     the trolley is how you know you have not missed anything. */
  .bought {
    color: var(--text-subtle);
    text-decoration: line-through;
  }

  .amount {
    font-variant-numeric: tabular-nums;
    white-space: nowrap;
  }
</style>
