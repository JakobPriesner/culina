<script lang="ts">
  import { onDestroy } from 'svelte';

  import { Button } from '$ds';
  import { explain } from '$shell/explain';
  import { m } from '$shell/i18n';
  import { toaster } from '$shell/toaster.svelte';

  import { shopping } from './stores/shopping.svelte';
  import { withdrawOffer } from './withdrawOffer.svelte';

  /**
   * Asks, where the lines are, what becomes of an unplanned meal's shopping (docs/woche-design.md §5.4). Its lines are
   * marked meanwhile; ticked ones stay either way, as they are already in the cupboard.
   */
  let { householdId }: { householdId: string } = $props();

  let busy = $state(false);

  const meal = $derived(withdrawOffer.meal);
  const lines = $derived(
    meal
      ? shopping.items.filter(
          (item) =>
            !item.isChecked &&
            (item.sources ?? []).some((source) => source.planEntryId === meal.entryId)
        ).length
      : 0
  );

  // Nothing left to take back (bought meanwhile, or taken off elsewhere): nothing to ask.
  $effect(() => {
    if (meal && shopping.status === 'ready' && lines === 0) withdrawOffer.settle();
  });

  // Leaving the list leaves the question too; the lines stay as they are.
  onDestroy(() => withdrawOffer.settle());

  async function withdraw() {
    if (!meal) return;

    busy = true;
    const failure = await shopping.withdrawMeal(householdId, meal.entryId);
    busy = false;

    if (failure) {
      toaster.show({ message: () => explain(failure), tone: 'danger' });

      return;
    }

    withdrawOffer.settle();
  }
</script>

{#if meal && lines > 0}
  <div class="note" role="status">
    <p>{m['shopping.unplanned.note']({ title: meal.title })}</p>
    <div class="actions">
      <Button size="sm" variant="primary" loading={busy} onclick={() => void withdraw()}>
        {m['shopping.unplanned.withdraw']({ count: lines })}
      </Button>
      <Button size="sm" variant="ghost" onclick={() => withdrawOffer.settle()}>
        {m['shopping.unplanned.keep']()}
      </Button>
    </div>
  </div>
{/if}

<style>
  .note {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
    margin-block: var(--space-3);
    padding: var(--space-3);
    border-radius: var(--radius-md);
    box-shadow: inset 3px 0 0 var(--accent);
    background: var(--surface-accent-subtle);
    font-size: var(--text-sm);
  }

  .actions {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2);
  }
</style>
