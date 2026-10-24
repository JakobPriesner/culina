<script lang="ts">
  import { IconButton } from '$ds';

  import { m } from '$shell/i18n';
  import { preferences } from '$shell/preferences.svelte';
  import { cookedOn, type PlannedDay, type PlannedMeal } from './mealPlan.svelte';
  import PlanDayNutrition from './PlanDayNutrition.svelte';
  import PlannedCard from './PlannedCard.svelte';
  import { dayDate, dayName, weekdayName } from './weekDates';
  import type { OnDrop, WeekDrag } from './weekDrag.svelte';

  interface Props {
    day: PlannedDay;
    today: boolean;
    /** Already over: still editable (people note what they ate), but quieter than the days ahead. */
    past: boolean;
    householdId: string | null;
    drag: WeekDrag;
    ondrop: OnDrop;
    onadd: () => void;
    onedit: (meal: PlannedMeal) => void;
    onremove: (meal: PlannedMeal) => void;
    /** Records a meal of today or earlier as cooked, in one tap. */
    onmadeit?: (meal: PlannedMeal) => void;
    /** Beside the list: shows a listed meal's lines there. */
    onlines?: (meal: PlannedMeal) => void;
    /** Beside the list: puts a meal not on it yet there. */
    ontolist?: (meal: PlannedMeal) => void;
    /** The day a list line led to, marked for a moment. */
    sought?: boolean;
    /** On a wide screen: a row of the week, its meals side by side (docs/woche-design.md §5.5). */
    row?: boolean;
  }

  let {
    day,
    today,
    past,
    householdId,
    drag,
    ondrop,
    onadd,
    onedit,
    onremove,
    onmadeit,
    onlines,
    ontolist,
    sought = false,
    row = false
  }: Props = $props();

  const landing = $derived(drag.landing?.date === day.date ? drag.landing : null);
</script>

<li
  class="day"
  class:today
  class:past
  class:over={landing !== null}
  class:sought
  class:row
  aria-current={today ? 'date' : undefined}
  data-plan-day={day.date}
  data-plan-row={row || undefined}
>
  <div class="top">
    <h2 class="name">
      <span class="label">{dayName(day.date, preferences.locale)}</span>
      <span class="date">{dayDate(day.date, preferences.locale)}</span>
    </h2>

    <!-- The day's "+" at the end of its line, as a list's section adds to itself: no row of its own for a button. -->
    <div class="add">
      <IconButton
        label={m['plan.addOn']({ day: weekdayName(day.date, preferences.locale) })}
        onclick={onadd}
      >
        <svg
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="2"
          aria-hidden="true"
        >
          <path d="M12 5v14M5 12h14" stroke-linecap="round" />
        </svg>
      </IconButton>
    </div>
  </div>

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
            cooked={cookedOn(meal, day.date)}
            lifted={drag.held?.entryId === meal.entryId}
            onpress={(event) =>
              drag.press(
                event,
                { entryId: meal.entryId, title: meal.title, from: day.date },
                ondrop
              )}
            onedit={() => onedit(meal)}
            onremove={() => onremove(meal)}
            onmadeit={onmadeit && (today || past) && !cookedOn(meal, day.date)
              ? () => onmadeit(meal)
              : undefined}
            onlines={onlines ? () => onlines(meal) : undefined}
            ontolist={ontolist ? () => ontolist(meal) : undefined}
            cookable={today}
          />
        </li>
      {/each}

      {#if landing && landing.position >= day.meals.length}
        <li class="seam" aria-hidden="true"></li>
      {/if}
    </ul>
  {/if}

  <PlanDayNutrition meals={day.meals} {householdId} />
</li>

<style>
  /*
   * A day of the agenda: its name and its "+" on one line, its meals under them. Days part by a hairline; today alone
   * has a fill (rounded, as a selected row in a list is), the days gone keep their meals but quieten their name.
   */
  .day {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
    min-width: 0;
    padding: var(--space-3);
    border-top: 1px solid var(--border);
    /* Lands clear of the floating header when a list line leads here. */
    scroll-margin-top: calc(var(--header-inset) + var(--space-4));
  }

  /* The day after today is another instance of this component: its previous sibling is matched globally. */
  .day:first-child,
  .today,
  :global(li.today) + .day {
    border-top-color: transparent;
  }

  .today {
    border-radius: var(--radius-lg);
    background: var(--surface-accent-subtle);
  }

  /* Outlined (today owns the fill) and inset so nothing shifts by a border width. */
  .over {
    outline: 2px dashed var(--border-focus);
    outline-offset: calc(-1 * var(--space-1));
    border-radius: var(--radius-lg);
  }

  /* The day a list line led to: ringed, so the eye lands on it. */
  .sought {
    border-radius: var(--radius-lg);
    box-shadow: 0 0 0 2px var(--accent);
  }

  /* Kept to the gap's height so the day does not grow by a card while dragging. */
  .seam {
    height: var(--space-1);
    border-radius: var(--radius-full);
    background: var(--border-focus);
  }

  .top {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-2);
    /* The "+" is a 44px target; its ink lines up with the cards' edge, its padding hangs into the day's. */
    margin-block: calc(-1 * var(--space-2));
    margin-inline-end: calc(-1 * var(--space-2));
  }

  .name {
    display: flex;
    flex-wrap: wrap;
    align-items: baseline;
    column-gap: var(--space-2);
    min-width: 0;
    margin: 0;
    font-size: var(--text-base);
    font-weight: var(--weight-semibold);
    line-height: var(--leading-tight);
  }

  .date {
    color: var(--text-muted);
    font-size: var(--text-sm);
    font-weight: var(--weight-regular);
    font-variant-numeric: tabular-nums;
  }

  .today .label {
    color: var(--accent);
  }

  /* Quieter than the days ahead, not dimmer: the name steps back, the meals stay readable. */
  .past .label,
  .past .date {
    color: var(--text-subtle);
  }

  .add {
    flex: none;
    color: var(--text-muted);
  }

  .meals {
    display: grid;
    grid-template-columns: minmax(0, 1fr);
    gap: var(--space-2);
    width: 100%;
    margin: 0;
    padding: 0;
    list-style: none;
  }

  /*
   * On a wide screen (docs/woche-design.md §5.5): a day is a row, its name in a fixed first column, its meals side by
   * side, its energy under them and its "+" at the end, so the whole week fits a laptop screen.
   */
  .row {
    display: grid;
    grid-template-columns: 8rem minmax(0, 1fr) auto;
    align-items: start;
    column-gap: var(--space-4);
    row-gap: var(--space-1);
  }

  .row .top {
    display: contents;
  }

  .row .name {
    grid-column: 1;
    grid-row: 1;
    flex-direction: column;
    align-items: flex-start;
    row-gap: 0;
    /* On the line of the first card's title. */
    padding-top: var(--space-2);
  }

  .row .meals {
    grid-column: 2;
    grid-row: 1;
    display: flex;
    flex-flow: row wrap;
  }

  .row .meals > [data-plan-meal] {
    flex: 1 1 15rem;
    min-width: 0;
    max-width: 24rem;
  }

  /* Narrower before "Kochen", the tick and the cross wrap under the title. */
  .row :global(.card .body) {
    flex-basis: 6rem;
  }

  /* Between cards side by side, the drop indicator stands up. */
  .row .seam {
    width: var(--space-1);
    height: auto;
    align-self: stretch;
  }

  .row :global([data-plan-nutrition]) {
    grid-column: 2;
    grid-row: 2;
  }

  .row .add {
    grid-column: 3;
    grid-row: 1;
  }

  /*
   * A mouse finds a card's tick and cross where it points, laid over the card's top end so they take no width from
   * the title; a finger has no hover, so there they stay drawn in the row. Hidden, they are not there at all (an
   * invisible layer would swallow clicks on "Auf der Einkaufsliste" beneath it), and only the buttons take the pointer.
   */
  @media (hover: hover) and (pointer: fine) {
    .row :global(.card .actions) {
      position: absolute;
      inset-block-start: 0;
      inset-inline-end: 0;
      align-items: center;
      padding-inline: var(--space-4) var(--space-1);
      border-radius: inherit;
      background: linear-gradient(to right, transparent, var(--surface-raised) var(--space-4));
      pointer-events: none;
    }

    .row :global(.card .actions > *) {
      pointer-events: auto;
    }

    .row :global(.card:not(:hover, :focus-within) .actions) {
      visibility: hidden;
    }
  }

  /* Tighter on a wide screen, so a whole week of rows fits a laptop beside the list. */
  .row {
    padding-block: var(--space-2);
  }

  .row :global(.card .body) {
    min-height: 2.75rem;
  }
</style>
