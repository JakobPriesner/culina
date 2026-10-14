<script lang="ts">
  import { Button, Checkbox, RadioGroup, SectionLabel, Sheet, Stepper } from '$ds';
  import type { RadioOption } from '$ds';

  import { m } from '$shell/i18n';
  import { preferences } from '$shell/preferences.svelte';

  import { servingsOf, type MealSlot, type PlannedDay, type PlannedMeal } from './mealPlan.svelte';
  import { mealSlots, slotLabel } from './slots';

  /**
   * One planned meal, answered rather than aimed at: where it goes, and for how many. The keyboard's and screen reader's only way to move it, and the only place
   * the slot can change (a drag that silently turned dinner into breakfast would be unaimable). Servings are this meal's own; the recipe keeps its.
   */
  interface Props {
    meal: PlannedMeal | null;
    days: readonly PlannedDay[];
    /** Which day it is on now, so the sheet opens on the answer it already has. */
    from: string;
    onsave: (to: { date: string; slot: MealSlot; servings: number; updateList: boolean }) => void;
    onclose: () => void;
  }

  let { meal, days, from, onsave, onclose }: Props = $props();

  const id = $props.id();

  let date = $state('');
  let slot = $state<MealSlot>('dinner');
  let servings = $state(1);
  let updateList = $state(true);

  /** The figure the stepper shows is whole; a meal planned for 7.4 keeps its 7.4 until somebody says otherwise. */
  let touched = $state(false);

  // Set here so opening the sheet for another meal starts from that meal's day.
  $effect(() => {
    if (meal) {
      date = from;
      slot = (meal.slot as MealSlot) ?? 'dinner';
      servings = Math.max(1, Math.round(servingsOf(meal)));
      updateList = true;
      touched = false;
    }
  });

  const answer = () =>
    onsave({
      date,
      slot,
      servings: meal && !touched ? servingsOf(meal) : servings,
      updateList
    });

  const weekdays = $derived(
    new Intl.DateTimeFormat(preferences.locale, { weekday: 'long', day: 'numeric', month: 'long' })
  );

  /** Midday, so a time zone west of UTC cannot move a date to the day before. */
  const dayOf = (day: string) => new Date(`${day}T12:00:00`);

  const dayOptions = $derived<readonly RadioOption[]>(
    days.map((day) => ({
      value: day.date,
      label: weekdays.format(dayOf(day.date)),
      description:
        day.meals.length > 0
          ? day.meals.map((one) => one.title).join(', ')
          : m['plan.move.freeDay']()
    }))
  );

  const slotOptions = $derived<readonly RadioOption[]>(
    mealSlots.map((which) => ({
      value: which,
      label: slotLabel[which]()
    }))
  );
</script>

<Sheet
  open={meal !== null}
  title={meal ? m['plan.edit.title']({ title: meal.title }) : ''}
  closeLabel={m['plan.move.close']()}
  {onclose}
>
  <div class="answers">
    {#if meal}
      <div class="servings">
        <SectionLabel as="span" spacing="tight">{m['recipe.servings.label']()}</SectionLabel>
        <Stepper
          id="{id}-servings"
          bind:value={servings}
          max={500}
          label={m['recipe.servings.label']()}
          decreaseLabel={m['recipe.servings.fewer']()}
          increaseLabel={m['recipe.servings.more']()}
          describedBy="{id}-servings-hint"
          onchange={() => (touched = true)}
        />
        <p class="hint" id="{id}-servings-hint">
          {m['plan.edit.servingsHint']({ count: meal.recipeServings })}
        </p>

        {#if meal.isOnShoppingList}
          <!-- The list holds a sum, so what it already has of this meal is said, and what following would and would not touch. -->
          <div class="list">
            <p class="hint">{m['plan.edit.onList']({ count: servingsOf(meal) })}</p>
            <Checkbox
              bind:checked={updateList}
              label={m['plan.edit.updateList']()}
              describedBy="{id}-list-hint"
            />
            <p class="hint" id="{id}-list-hint">{m['plan.edit.updateListHint']()}</p>
          </div>
        {/if}
      </div>
    {/if}

    <fieldset>
      <SectionLabel as="legend" spacing="tight">{m['plan.move.day']()}</SectionLabel>
      <RadioGroup name="move-day" bind:value={date} options={dayOptions} />
    </fieldset>

    <fieldset>
      <SectionLabel as="legend" spacing="tight">{m['plan.pick.slot']()}</SectionLabel>
      <RadioGroup name="move-slot" bind:value={slot} options={slotOptions} />
    </fieldset>
  </div>

  {#snippet footer()}
    <Button onclick={onclose}>{m['plan.move.cancel']()}</Button>
    <Button variant="primary" onclick={answer}>
      {m['plan.edit.confirm']()}
    </Button>
  {/snippet}
</Sheet>

<style>
  .answers {
    display: flex;
    flex-direction: column;
    gap: var(--space-6);
  }

  fieldset {
    min-width: 0;
    padding: 0;
    border: 0;
  }

  .servings,
  .list {
    display: flex;
    flex-direction: column;
    align-items: flex-start;
    gap: var(--space-2);
  }

  .hint {
    margin: 0;
    color: var(--text-muted);
    font-size: var(--text-sm);
  }
</style>
