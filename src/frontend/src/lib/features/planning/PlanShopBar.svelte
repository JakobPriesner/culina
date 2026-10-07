<script lang="ts">
  import { resolve } from '$app/paths';
  import { Button } from '$ds';

  import { m } from '$shell/i18n';

  interface Props {
    /** How many planned meals are not on the shopping list yet. */
    unshopped: number;
    busy: boolean;
    onshop: () => void;
  }

  let { unshopped, busy, onshop }: Props = $props();
</script>

<div class="shop">
  {#if unshopped > 0}
    <Button variant="primary" loading={busy} onclick={onshop}>
      {m['plan.toShoppingList']({ count: unshopped })}
    </Button>
    <p class="shop-note">{m['plan.shop.hint']()}</p>
  {:else}
    <p class="shop-note" role="status">
      {m['plan.shop.done']()}
      <a href={resolve('/(app)/shopping')}>{m['plan.openList']()}</a>
    </p>
  {/if}
</div>

<style>
  .shop {
    display: flex;
    flex-direction: column;
    align-items: flex-start;
    gap: var(--space-2);
    margin-top: var(--space-6);
  }

  .shop-note {
    max-width: var(--measure);
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .shop-note a {
    color: var(--text);
    font-weight: var(--weight-semibold);
  }
</style>
