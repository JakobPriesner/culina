<script lang="ts">
  import { Button } from '$ds';
  import { explain } from '$shell/explain';
  import { formatNumber, m } from '$shell/i18n';
  import { toaster } from '$shell/toaster.svelte';

  import { householdFoods as store, type HouseholdFood } from './stores/householdFoods.svelte';

  /**
   * The foods the household typed from a package. They are added where they are needed, in an ingredient's
   * nutrition sheet; here they can be removed, which takes every choice of them along. One a household it
   * inherits from typed is that household's to remove. The page loads the store.
   */
  async function remove(food: HouseholdFood, householdId: string) {
    const result = await store.remove(householdId, food.id);

    toaster.show(
      result.ok
        ? { message: () => m['nutrition.package.removed']({ name: food.name }), tone: 'success' }
        : { message: () => explain(result.error), tone: 'danger' }
    );
  }

  interface Props {
    householdId: string;
    /** After a removal, for what else on the page shows the choices of it. */
    onremoved?: () => void;
  }

  let { householdId, onremoved }: Props = $props();
</script>

{#if store.foods.length === 0}
  <p class="quiet">{m['nutrition.package.empty']()}</p>
{:else}
  <ul class="rows" aria-label={m['nutrition.package.listTitle']()}>
    {#each store.foods as food (food.id)}
      <li class="row">
        <span class="words">
          <span>{food.name}</span>
          <span class="quiet">
            {[
              m['nutrition.package.per100']({ kcal: formatNumber(food.energyKcal) }),
              food.own ? null : m['nutrition.package.inherited']()
            ]
              .filter(Boolean)
              .join(' · ')}
          </span>
        </span>
        {#if food.own}
          <Button
            size="sm"
            variant="ghost"
            label={m['nutrition.package.removeLabel']({ name: food.name })}
            onclick={async () => {
              await remove(food, householdId);
              onremoved?.();
            }}
          >
            {m['nutrition.package.remove']()}
          </Button>
        {/if}
      </li>
    {/each}
  </ul>
{/if}

<style>
  .quiet {
    margin: 0;
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .rows {
    max-width: var(--measure);
    margin: 0;
    padding: 0;
    list-style: none;
  }

  .row {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-2) var(--space-4);
    min-height: var(--control-md);
    padding-block: var(--space-2);
    border-bottom: 1px solid var(--border);
  }

  .words {
    display: flex;
    flex-direction: column;
    min-width: 0;
    overflow-wrap: anywhere;
  }
</style>
