<script lang="ts">
  import type { Snippet } from 'svelte';

  import { m } from '$shell/i18n';
  import { preferences } from '$shell/preferences.svelte';

  import { dateToday, type PlannedDay, type PlannedMeal } from './mealPlan.svelte';
  import PlanDay from './PlanDay.svelte';
  import { rangeOf } from './weekDates';
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
    /** On a wide screen: the days as rows, so the whole week fits beside the list (docs/woche-design.md §5.5). */
    rows?: boolean;
    /** Beside the list: shows a listed meal's lines there. */
    onlines?: (meal: PlannedMeal) => void;
    /** Beside the list: puts a meal not on it yet there. */
    ontolist?: (meal: PlannedMeal) => void;
    /** A day to bring into view (a list line led here), marked while it is shown. */
    seek?: string | null;
    /** The days before today fold into one line, which `onunfold` opens. */
    folded?: boolean;
    onunfold?: () => void;
    /** Today's own line from elsewhere (the diary), drawn inside today's day rather than as a row above the week. */
    todayExtra?: Snippet;
  }

  let {
    days,
    householdId,
    drag,
    ondrop,
    onadd,
    onedit,
    onremove,
    onmadeit,
    rows = false,
    onlines,
    ontolist,
    seek = null,
    folded = false,
    onunfold,
    todayExtra
  }: Props = $props();

  const today = dateToday();

  /** The days already over that fold away: only ones before today, and never the day a list line led to. */
  const earlier = $derived(
    folded ? days.filter((day) => day.date < today && day.date !== seek) : []
  );
  const shown = $derived(earlier.length > 0 ? days.filter((day) => !earlier.includes(day)) : days);
  const earlierMeals = $derived(earlier.reduce((sum, day) => sum + day.meals.length, 0));

  /** Plain, not state: each day asked for is brought into view once. */
  let shownSought: string | null = null;

  // A list line leads to its day: brought into view under the header once it has rendered.
  // An attachment re-runs when `days` changes, which is how it waits for the days to arrive.
  function bringSought(list: HTMLOListElement) {
    if (!seek || shownSought === seek || !days.some((day) => day.date === seek)) return;

    shownSought = seek;
    requestAnimationFrame(() =>
      list
        .querySelector<HTMLElement>(`[data-plan-day="${seek}"]`)
        ?.scrollIntoView({ block: 'start' })
    );
  }
</script>

<ol class="week" class:rows class:dragging={drag.held !== null} {@attach bringSought}>
  {#if earlier.length > 0}
    <!-- On a Saturday, Monday is history: one quiet line says what it held, and opens it in place. -->
    <li class="earlier">
      <button type="button" class="unfold" aria-expanded="false" onclick={onunfold}>
        <span class="earlier-title">{m['plan.earlier.title']()}</span>
        <span class="earlier-sum">
          {rangeOf(earlier[0]!.date, preferences.locale, earlier.length)} ·
          {earlierMeals > 0
            ? m['plan.earlier.meals']({ count: earlierMeals })
            : m['plan.earlier.none']()}
        </span>
        <svg
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="2"
          aria-hidden="true"
        >
          <path d="m7 10 5 5 5-5" stroke-linecap="round" stroke-linejoin="round" />
        </svg>
      </button>
    </li>
  {/if}
  {#each shown as day (day.date)}
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
      {onlines}
      {ontolist}
      sought={day.date === seek}
      row={rows}
      extra={day.date === today ? todayExtra : undefined}
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

  /*
   * One surface for the days, each parted from the next by a hairline: an agenda, read top to bottom, not seven
   * boxes. Today is the one day with a fill, so the eye starts there.
   */
  .week {
    display: flex;
    flex-direction: column;
    margin: 0;
    padding: 0;
    list-style: none;
  }

  .earlier {
    border-bottom: 1px solid var(--border);
  }

  .unfold {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    column-gap: var(--space-2);
    width: 100%;
    min-height: var(--control-md);
    padding: var(--space-2) var(--space-3);
    border: none;
    border-radius: var(--radius-md);
    background: none;
    color: var(--text-muted);
    font: inherit;
    font-size: var(--text-sm);
    text-align: start;
    cursor: pointer;
  }

  .unfold:hover {
    background: var(--surface-hover);
    color: var(--text);
  }

  .unfold:focus-visible {
    outline: 2px solid var(--border-focus);
    outline-offset: -2px;
  }

  .earlier-title {
    font-weight: var(--weight-semibold);
  }

  .earlier-sum {
    color: var(--text-subtle);
    font-variant-numeric: tabular-nums;
  }

  .unfold svg {
    width: 1rem;
    height: 1rem;
    margin-inline-start: auto;
  }
</style>
