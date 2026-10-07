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
    /**
     * What is already on this week.
     *
     * Passed on so nothing on the plan is offered again: a household that has
     * already agreed to cook the lasagne on Tuesday does not want it suggested
     * for Thursday as well. The picker also uses it to mark a recipe as taken
     * when somebody searches for one by name.
     */
    taken: string[];
    /** Which meal the pick is for. */
    slot: MealSlot;
    onpick: (recipe: RecipeSummary) => void;
    onclose: () => void;
  }

  let { open, householdId, taken, slot = $bindable(), onpick, onclose }: Props = $props();

  /** Which shelf the picker is searching, or null for everything. */
  let narrowedTo = $state<string | null>(null);

  // The shelves, so the picker can offer to narrow to one. Only asked for once
  // the picker is opened.
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
  <!-- Which meal, and only here. A slot picker on the week view would put
       three empty rows on every day for the household that only plans
       dinner, which is most of them. -->
  {#snippet controls()}
    <!-- Narrowing to a shelf, and only when the household has one. "What are
         we cooking Thursday" is usually asked of a subset somebody has
         already chosen, and this is that subset. -->
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
