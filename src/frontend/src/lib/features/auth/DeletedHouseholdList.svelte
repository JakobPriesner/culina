<script lang="ts">
  import { goto } from '$app/navigation';
  import { resolve } from '$app/paths';
  import { Button } from '$ds';
  import { explain } from '$shell/explain';
  import { formatDate, m } from '$shell/i18n';
  import { toaster } from '$shell/toaster.svelte';

  import { restoreHousehold, type DeletedHousehold } from './households.svelte';
  import { session } from './session.svelte';

  /**
   * Households this person deleted as an owner and can still restore; also on the welcome screen, since
   * someone who deleted their only household lands there. The server lists only the caller's own.
   */
  interface Props {
    items: readonly DeletedHousehold[];
  }

  let { items }: Props = $props();

  let restoring = $state<string | null>(null);

  async function restore(household: DeletedHousehold) {
    restoring = household.householdId;

    const failure = await restoreHousehold(household.householdId);

    restoring = null;

    if (failure) {
      toaster.show({ message: () => explain(failure), tone: 'danger' });

      return;
    }

    await session.refresh();
    session.selectHousehold(household.householdId);
    toaster.show({
      message: () => m['trash.restored']({ name: household.name }),
      tone: 'success'
    });
    await goto(resolve('/(app)/me/household'));
  }
</script>

<ul class="list" data-testid="deleted-households">
  {#each items as household (household.householdId)}
    <li class="row">
      <div class="what">
        <p class="name">{household.name}</p>
        {#if household.purgeAfter}
          <p class="when">
            {m['trash.until']({
              when: formatDate(new Date(household.purgeAfter), { dateStyle: 'long' })
            })}
          </p>
        {/if}
      </div>
      <Button onclick={() => restore(household)} loading={restoring === household.householdId}>
        {m['trash.restore']()}
      </Button>
    </li>
  {/each}
</ul>

<style>
  .list {
    display: flex;
    flex-direction: column;
    width: 100%;
    margin: 0;
    padding: 0;
    border: 1px solid var(--border);
    border-radius: var(--radius-lg);
    list-style: none;
  }

  .row {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-2) var(--space-4);
    padding: var(--space-3) var(--space-3) var(--space-3) var(--space-4);
  }

  .row + .row {
    border-top: 1px solid var(--border);
  }

  .what {
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
    min-width: 0;
  }

  .name {
    font-weight: var(--weight-semibold);
    overflow-wrap: anywhere;
  }

  .when {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }
</style>
