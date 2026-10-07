<script lang="ts">
  import { goto } from '$app/navigation';
  import { resolve } from '$app/paths';
  import { Button, Skeleton } from '$ds';
  import type { components } from '$api/generated/schema';
  import { explain } from '$shell/explain';
  import { formatDate, m } from '$shell/i18n';
  import { toaster } from '$shell/toaster.svelte';

  import { readTrash, restoreCookbook, restoreRecipe } from './trash';

  /**
   * What was deleted in this household lately, with the way back; says plainly when it is gone for good,
   * and restoring opens the thing restored.
   */
  interface Props {
    householdId: string;
  }

  let { householdId }: Props = $props();

  type Item = components['schemas']['HouseholdsGetTrashTrashItem'];

  let items = $state<Item[] | null>(null);
  let failed = $state(false);
  let restoring = $state<string | null>(null);

  $effect(() => {
    void load(householdId);
  });

  async function load(id: string) {
    const result = await readTrash(id);

    failed = !result.ok;
    items = result.ok ? [...result.value.items] : [];
  }

  async function restore(item: Item) {
    restoring = item.id;

    const failure =
      item.kind === 'cookbook' ? await restoreCookbook(item.id) : await restoreRecipe(item.id);

    restoring = null;

    if (failure) {
      toaster.show({ message: () => explain(failure), tone: 'danger' });
      await load(householdId);

      return;
    }

    toaster.show({ message: () => m['trash.restored']({ name: item.name }), tone: 'success' });

    await goto(
      item.kind === 'cookbook'
        ? resolve('/(app)/cookbooks/[cookbookId]', { cookbookId: item.id })
        : resolve('/(app)/recipes/[recipeId]', { recipeId: item.id })
    );
  }
</script>

{#if items === null}
  <div aria-busy="true">
    <Skeleton width="100%" height="3.5rem" shape="block" />
  </div>
{:else if failed}
  <p class="empty">{m['trash.failed']()}</p>
{:else if items.length === 0}
  <p class="empty">{m['trash.empty']()}</p>
{:else}
  <ul class="list" data-testid="trash">
    {#each items as item (item.id)}
      <li class="row">
        <div class="what">
          <p class="name">{item.name}</p>
          <p class="when">
            {item.kind === 'cookbook' ? m['trash.kind.cookbook']() : m['trash.kind.recipe']()}
            ·
            {item.deletedBy ? m['trash.deletedBy']({ who: item.deletedBy }) : m['trash.deleted']()}
            ·
            {m['trash.until']({
              when: formatDate(new Date(item.purgeAfter), { dateStyle: 'long' })
            })}
          </p>
        </div>
        <Button onclick={() => restore(item)} loading={restoring === item.id}>
          {m['trash.restore']()}
        </Button>
      </li>
    {/each}
  </ul>
{/if}

<style>
  .empty {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  /* Hairlines between rows, like the invitations: one list, not a stack of boxes. */
  .list {
    display: flex;
    flex-direction: column;
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
