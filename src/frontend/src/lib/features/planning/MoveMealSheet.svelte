<script lang="ts">
  import { Button, RadioGroup, Sheet } from '$ds';
  import type { RadioOption } from '$ds';

  import { m } from '$shell/i18n';
  import { preferences } from '$shell/preferences.svelte';

  import type { MealSlot, PlannedDay, PlannedMeal } from './mealPlan.svelte';

  /**
   * Where a meal goes, answered rather than aimed at.
   *
   * The same move as the drag, and deliberately not a lesser one: it is the
   * only way a keyboard has, the only way a screen reader has, and on a phone
   * it is the better way whenever the day you want is not on screen. It is also
   * the only place the slot can be changed, because a drag that quietly turned
   * a dinner into a breakfast would be a drag nobody could aim.
   */
  interface Props {
    /** The meal being moved, or null when nothing is. */
    meal: PlannedMeal | null;
    /** The week on screen. A move reaches the days you can see. */
    days: readonly PlannedDay[];
    /** Which day it is on now, so the sheet opens on the answer it already has. */
    from: string;
    onmove: (to: { date: string; slot: MealSlot }) => void;
    onclose: () => void;
  }

  let { meal, days, from, onmove, onclose }: Props = $props();

  let date = $state('');
  let slot = $state<MealSlot>('dinner');

  // Set here rather than initialised above, so that opening the sheet again for
  // another meal starts from that meal's day. Answers left over from the last
  // one are answers about something else.
  $effect(() => {
    if (meal) {
      date = from;
      slot = (meal.slot as MealSlot) ?? 'dinner';
    }
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
      // What is already there, so the choice is made against the week rather
      // than against seven dates.
      description:
        day.meals.length > 0
          ? day.meals.map((one) => one.title).join(', ')
          : m['plan.move.freeDay']()
    }))
  );

  const slotOptions = $derived<readonly RadioOption[]>(
    (['breakfast', 'lunch', 'dinner'] as const).map((which) => ({
      value: which,
      label: m[`plan.slot.${which}`]()
    }))
  );
</script>

<Sheet
  open={meal !== null}
  title={meal ? m['plan.move.title']({ title: meal.title }) : ''}
  closeLabel={m['plan.move.close']()}
  {onclose}
>
  <div class="answers">
    <fieldset>
      <legend>{m['plan.move.day']()}</legend>
      <RadioGroup name="move-day" bind:value={date} options={dayOptions} />
    </fieldset>

    <fieldset>
      <legend>{m['plan.pick.slot']()}</legend>
      <RadioGroup name="move-slot" bind:value={slot} options={slotOptions} />
    </fieldset>
  </div>

  {#snippet footer()}
    <Button onclick={onclose}>{m['plan.move.cancel']()}</Button>
    <Button variant="primary" onclick={() => onmove({ date, slot })}>
      {m['plan.move.confirm']()}
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

  legend {
    margin-bottom: var(--space-2);
    color: var(--text-subtle);
    font-size: var(--text-xs);
    letter-spacing: 0.08em;
    text-transform: uppercase;
  }
</style>
