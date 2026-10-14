<script lang="ts">
  import { SectionLabel } from '$ds';

  import { cookbooks } from '$features/cookbooks/stores/cookbooks.svelte';
  import RecipePicker from '$features/recipes/RecipePicker.svelte';
  import type { RecipeSummary } from '$features/recipes/types';
  import { formatDate, m } from '$shell/i18n';
  import type { MealSlot, PlannedDay } from './mealPlan.svelte';
  import { mealSlots, slotLabel } from './slots';
  import { dayOf } from './weekDates';

  interface Props {
    open: boolean;
    householdId: string;
    /** The day being planned, which the sheet says in its title: on a phone it covers the week it was opened from. */
    date: string | null;
    /** The week on screen: what is already planned in it is not offered again unasked, and says on which days when searched by name. */
    days: readonly PlannedDay[];
    slot: MealSlot;
    onpick: (recipe: RecipeSummary) => void;
    onclose: () => void;
  }

  let { open, householdId, date, days, slot = $bindable(), onpick, onclose }: Props = $props();

  let narrowedTo = $state<string | null>(null);

  const longDay = (day: string) =>
    formatDate(dayOf(day), { weekday: 'long', day: 'numeric', month: 'long' });

  const meals = $derived(
    days.flatMap((day) => day.meals.map((meal) => ({ ...meal, date: day.date })))
  );

  const taken = $derived([...new Set(meals.map((meal) => meal.recipeId))]);

  const when = (day: string) =>
    formatDate(dayOf(day), { weekday: 'short', day: 'numeric', month: 'short' });

  /** Where each planned recipe already is, so "Added" is not all a row says and picking it again is understood as adding another. */
  const notes = $derived(
    new Map(
      taken.map((recipeId) => {
        const where = meals
          .filter((meal) => meal.recipeId === recipeId)
          .map((meal) => `${when(meal.date)} · ${slotLabel[meal.slot as MealSlot]?.() ?? ''}`);

        const first = where.slice(0, 2).join(', ');

        return [
          recipeId,
          where.length > 2
            ? m['plan.pick.plannedMore']({ when: first, count: where.length - 2 })
            : m['plan.pick.planned']({ when: first })
        ];
      })
    )
  );

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
  title={date ? m['plan.pick.titleFor']({ day: longDay(date) }) : m['plan.pick.title']()}
  cookbookId={narrowedTo ?? undefined}
  suggestFor={slot}
  {taken}
  {notes}
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
      <SectionLabel as="legend">{m['plan.pick.slot']()}</SectionLabel>
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

  .slots label {
    min-height: var(--control-sm);
    display: flex;
    align-items: center;
    gap: var(--space-2);
  }
</style>
