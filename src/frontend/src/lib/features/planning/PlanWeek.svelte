<script lang="ts">
  import { asDate, type PlannedDay, type PlannedMeal } from './mealPlan.svelte';
  import PlanDay from './PlanDay.svelte';
  import type { OnDrop, WeekDrag } from './weekDrag.svelte';

  interface Props {
    days: readonly PlannedDay[];
    drag: WeekDrag;
    ondrop: OnDrop;
    onadd: (date: string) => void;
    onmove: (meal: PlannedMeal) => void;
    onremove: (meal: PlannedMeal) => void;
  }

  let { days, drag, ondrop, onadd, onmove, onremove }: Props = $props();

  const today = asDate(new Date());
</script>

<ol class="week" class:dragging={drag.held !== null}>
  {#each days as day (day.date)}
    <PlanDay
      {day}
      today={day.date === today}
      {drag}
      {ondrop}
      onadd={() => onadd(day.date)}
      {onmove}
      {onremove}
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
