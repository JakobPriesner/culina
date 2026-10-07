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
        <!-- The line the card would land on. Drawn between the cards
             rather than around the day, because a day is the answer to
             "which day" and this is the answer to "where in it". -->
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

  <!-- Every day offers it, planned or not. An "add" that only appears
       on an empty day is an add you cannot use twice. -->
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

  /* Lit rather than boxed: today keeps its place in the row and the eye finds
     it without the layout moving. */
  .today {
    background: var(--surface-accent-subtle);
  }

  /* Outlined rather than filled, because today already owns the filled one and
     today is a day you can drop on. Inset, so the seven columns do not shift
     by a border's width as a card crosses them. */
  .over {
    outline: 2px dashed var(--border-focus);
    outline-offset: calc(-1 * var(--space-1));
  }

  /* Where it would land. Kept to the height of the gap it opens so the day does
     not grow by a whole card as the pointer crosses it. */
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
