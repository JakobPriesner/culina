<script lang="ts">
  import { cookbooks } from '$features/cookbooks/stores/cookbooks.svelte';
  import RecipePicker from '$features/recipes/RecipePicker.svelte';
  import type { RecipeSummary } from '$features/recipes/types';
  import { m } from '$shell/i18n';
  import type { MealSlot } from './mealPlan.svelte';
  import { mealSlots, slotLabel } from './slots';

  interface Props {
    open: boolean;
    householdId: string;
    /** Passed on so nothing already planned is offered again, and marked taken when searched by name. */
    taken: string[];
    slot: MealSlot;
    onpick: (recipe: RecipeSummary) => void;
    onclose: () => void;
  }

  let { open, householdId, taken, slot = $bindable(), onpick, onclose }: Props = $props();

  let narrowedTo = $state<string | null>(null);

  // Shelves are fetched only once the picker opens.
  $effect(() => {
    if (open) {
      void cookbooks.list(householdId);
    }
  });
</script>

<RecipePicker
  {open}
  {householdId}
  title={m['plan.pick.title']()}
  cookbookId={narrowedTo ?? undefined}
  suggestFor={slot}
  {taken}
  {onpick}
  {onclose}
>
  <!-- The slot picker lives only here: on the week view it would add three empty rows per day. -->
  {#snippet controls()}
    {#if cookbooks.items.length > 0}
      <fieldset class="slots">
        <legend>{m['cookbooks.title']()}</legend>
        <label>
          <input type="radio" name="cookbook" value={null} bind:group={narrowedTo} />
          {m['cookbooks.picker.all']()}
        </label>
        {#each cookbooks.items as cookbook (cookbook.id)}
          <label>
            <input type="radio" name="cookbook" value={cookbook.id} bind:group={narrowedTo} />
            {cookbook.name}
          </label>
        {/each}
      </fieldset>
    {/if}

    <fieldset class="slots">
      <legend>{m['plan.pick.slot']()}</legend>
      {#each mealSlots as which (which)}
        <label>
          <input type="radio" name="slot" value={which} bind:group={slot} />
          {slotLabel[which]()}
        </label>
      {/each}
    </fieldset>
  {/snippet}
</RecipePicker>

<style>
  .slots {
    display: flex;
    flex-wrap: wrap;
    min-width: 0;
    gap: var(--space-4);
    padding: 0;
    border: 0;
  }

  .slots legend {
    color: var(--text-subtle);
    font-size: var(--text-xs);
    letter-spacing: 0.08em;
    text-transform: uppercase;
  }

  .slots label {
    min-height: var(--control-sm);
    display: flex;
    align-items: center;
    gap: var(--space-2);
  }
</style>
