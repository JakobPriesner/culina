<script lang="ts">
  import { Field, SectionLabel, SegmentedControl, Select } from '$ds';

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

  /** One row however many shelves a kitchen has; "" is every recipe. */
  const shelves = $derived([
    { value: '', label: m['cookbooks.picker.all']() },
    ...cookbooks.items.map((cookbook) => ({ value: cookbook.id, label: cookbook.name }))
  ]);

  const segments = $derived(mealSlots.map((which) => ({ id: which, label: slotLabel[which]() })));

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
  stubs
  {taken}
  {notes}
  {onpick}
  {onclose}
>
  <!-- The slot picker lives only here: on the week view it would add three empty rows per day. -->
  {#snippet controls()}
    <!-- The app's own controls, as on the plan's other sheet: native radios here wrapped into uneven rows and looked like another app. -->
    {#if cookbooks.items.length > 0}
      <Field label={m['cookbooks.title']()}>
        {#snippet children({ id })}
          <Select
            {id}
            value={narrowedTo ?? ''}
            options={shelves}
            onchange={(value) => (narrowedTo = value || null)}
          />
        {/snippet}
      </Field>
    {/if}

    <div class="slot">
      <SectionLabel tone="muted" as="span" spacing="tight">{m['plan.pick.slot']()}</SectionLabel>
      <SegmentedControl
        {segments}
        selected={slot}
        label={m['plan.pick.slot']()}
        onselect={(id) => (slot = id as MealSlot)}
      />
    </div>
  {/snippet}
</RecipePicker>

<style>
  .slot {
    display: flex;
    flex-direction: column;
    min-width: 0;
  }
</style>
