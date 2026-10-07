<script lang="ts">
  import { Button } from '$ds';

  import { m } from '$shell/i18n';
  import { preferences } from '$shell/preferences.svelte';
  import type { PlannedDay, PlannedMeal } from './mealPlan.svelte';
  import PlannedCard from './PlannedCard.svelte';
  import { weekdayName } from './weekDates';
  import type { OnDrop, WeekDrag } from './weekDrag.svelte';

  interface Props {
    day: PlannedDay;
    today: boolean;
    drag: WeekDrag;
    ondrop: OnDrop;
    onadd: () => void;
    onmove: (meal: PlannedMeal) => void;
    onremove: (meal: PlannedMeal) => void;
  }

  let { day, today, drag, ondrop, onadd, onmove, onremove }: Props = $props();

  const landing = $derived(drag.landing?.date === day.date ? drag.landing : null);
</script>

<li class="day" class:today class:over={landing !== null} data-plan-day={day.date}>
  <h2 class="name">
    {weekdayName(day.date, preferences.locale)}
    <span class="number">{Number(day.date.slice(-2))}</span>
  </h2>

  {#if day.meals.length > 0 || landing}
    <ul class="meals">
      {#each day.meals as meal, index (meal.entryId)}
        <!-- Drop indicator between cards. -->
        {#if landing?.position === index}
          <li class="seam" aria-hidden="true"></li>
        {/if}

        <li data-plan-meal>
          <PlannedCard
            {meal}
            lifted={drag.held?.entryId === meal.entryId}
            onpress={(event) =>
              drag.press(
                event,
                { entryId: meal.entryId, title: meal.title, from: day.date },
                ondrop
              )}
            onmove={() => onmove(meal)}
            onremove={() => onremove(meal)}
          />
        </li>
      {/each}

      {#if landing && landing.position >= day.meals.length}
        <li class="seam" aria-hidden="true"></li>
      {/if}
    </ul>
  {/if}

  <Button size="sm" variant="ghost" onclick={onadd}>+ {m['plan.add']()}</Button>
</li>

<style>
  .day {
    display: flex;
    flex-direction: column;
    align-items: flex-start;
    gap: var(--space-2);
    min-width: 0;
    padding: var(--space-3);
    border-radius: var(--radius-md);
    background: var(--surface-sunken);
  }

  .today {
    background: var(--surface-accent-subtle);
  }

  /* Outlined (today owns the fill) and inset so columns do not shift by a border width. */
  .over {
    outline: 2px dashed var(--border-focus);
    outline-offset: calc(-1 * var(--space-1));
  }

  /* Kept to the gap's height so the day does not grow by a card while dragging. */
  .seam {
    height: var(--space-1);
    border-radius: var(--radius-full);
    background: var(--border-focus);
  }

  .name {
    display: flex;
    align-items: baseline;
    flex-wrap: wrap;
    gap: var(--space-2);
    color: var(--text-subtle);
    font-size: var(--text-xs);
    letter-spacing: 0.04em;
    text-transform: uppercase;
  }

  .number {
    color: var(--text-muted);
    font-size: var(--text-sm);
    font-variant-numeric: tabular-nums;
    letter-spacing: normal;
  }

  .meals {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
    width: 100%;
    margin: 0;
    padding: 0;
    list-style: none;
  }

  @media (min-width: 80rem) {
    .name {
      flex-direction: column;
    }
  }
</style>
