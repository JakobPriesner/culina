<script lang="ts">
  import { asDate, type PlannedDay, type PlannedMeal } from './mealPlan.svelte';
  import PlanDay from './PlanDay.svelte';
  import type { OnDrop, WeekDrag } from './weekDrag.svelte';

  interface Props {
    days: readonly PlannedDay[];
    householdId: string | null;
    drag: WeekDrag;
    ondrop: OnDrop;
    onadd: (date: string) => void;
    onedit: (meal: PlannedMeal) => void;
    onremove: (meal: PlannedMeal) => void;
    onmadeit?: (meal: PlannedMeal) => void;
  }

  let { days, householdId, drag, ondrop, onadd, onedit, onremove, onmadeit }: Props = $props();

  const today = asDate(new Date());

  /** Plain, not state: the week opens at today once per visit, never again while someone scrolls. */
  let shownToday = false;

  // On a phone the days stack, so today can sit below a screen of days already over; start there instead.
  // An attachment re-runs when `days` changes, which is how it waits for the week to arrive.
  function startAtToday(list: HTMLOListElement) {
    if (shownToday || !days.some((day) => day.date === today)) {
      return;
    }

    shownToday = true;

    if (!matchMedia('(min-width: 40rem)').matches) {
      // After the days have rendered, which happens in the same update as this run.
      requestAnimationFrame(() =>
        list.querySelector<HTMLElement>('[aria-current="date"]')?.scrollIntoView({ block: 'start' })
      );
    }
  }
</script>

<ol class="week" class:dragging={drag.held !== null} {@attach startAtToday}>
  {#each days as day (day.date)}
    <PlanDay
      {day}
      today={day.date === today}
      past={day.date < today}
      {householdId}
      {drag}
      {ondrop}
      onadd={() => onadd(day.date)}
      {onedit}
      {onremove}
      {onmadeit}
    />
  {/each}
</ol>

<style>
  /* A long press is this list's own gesture: no iOS callout menu. */
  .week {
    -webkit-touch-callout: none;
  }

  /* No text selection while dragging across the week. */
  .dragging {
    -webkit-user-select: none;
    user-select: none;
  }

  /* Day cards stay chronological; seven columns only when each day is usable. */
  .week {
    display: grid;
    grid-template-columns: minmax(0, 1fr);
    gap: var(--space-3);
    margin: 0;
    padding: 0;
    list-style: none;
  }

  @media (min-width: 40rem) {
    .week {
      grid-template-columns: repeat(2, minmax(0, 1fr));
    }
  }

  @media (min-width: 64rem) {
    .week {
      grid-template-columns: repeat(3, minmax(0, 1fr));
    }
  }

  @media (min-width: 80rem) {
    .week {
      grid-template-columns: repeat(7, minmax(0, 1fr));
    }
  }
</style>
