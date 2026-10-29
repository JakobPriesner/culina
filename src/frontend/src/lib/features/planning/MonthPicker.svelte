<script lang="ts">
  import { Popover } from '$ds';

  import { m } from '$shell/i18n';

  import { asDate, dateToday, plusDays, weekStart } from './mealPlan.svelte';
  import { dayOf } from './weekDates';

  /**
   * The month as a way to get somewhere, not a place to plan (docs/woche-design.md §10): the range under the title opens
   * it, a day in it shows the plan from that day. Monday first, like the week; today filled, the days on screen tinted.
   */
  interface Props {
    /** The days on screen, worded: the trigger. */
    range: string;
    /** The first and last day on screen, tinted in the month. */
    from: string;
    to: string;
    locale: string;
    onpick: (date: string) => void;
  }

  let { range, from, to, locale, onpick }: Props = $props();

  /** The first of the month on show; it follows the days on screen until someone pages the month. */
  let paged = $state<string | null>(null);
  const month = $derived(paged ?? `${from.slice(0, 7)}-01`);

  const today = dateToday();

  const title = $derived(
    new Intl.DateTimeFormat(locale, { month: 'long', year: 'numeric' }).format(dayOf(month))
  );

  const weekdays = $derived(
    Array.from({ length: 7 }, (_, index) =>
      new Intl.DateTimeFormat(locale, { weekday: 'narrow' }).format(
        dayOf(plusDays('2024-01-01', index))
      )
    )
  );

  /** Whole weeks, Monday to Sunday, from the week holding the 1st to the week holding the last day. */
  const days = $derived.by(() => {
    const first = weekStart(month);
    const next = asDate(new Date(dayOf(month).getFullYear(), dayOf(month).getMonth() + 1, 1));
    const last = plusDays(weekStart(plusDays(next, -1)), 6);
    const all: string[] = [];

    for (let day = first; day <= last; day = plusDays(day, 1)) all.push(day);

    return all;
  });

  function page(months: number) {
    const start = dayOf(month);

    paged = asDate(new Date(start.getFullYear(), start.getMonth() + months, 1));
  }

  function pick(event: MouseEvent, date: string) {
    (event.currentTarget as HTMLElement).closest<HTMLElement>('[popover]')?.hidePopover();
    paged = null;
    onpick(date);
  }
</script>

<Popover placement="bottom-start">
  {#snippet trigger({ popovertarget })}
    <button
      type="button"
      class="trigger"
      {popovertarget}
      aria-label={`${range}. ${m['plan.jump.label']()}`}
    >
      {range}
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
  {/snippet}

  <div class="month">
    <div class="head">
      <button
        type="button"
        class="page"
        aria-label={m['plan.jump.previousMonth']()}
        onclick={() => page(-1)}
      >
        <svg
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="2"
          aria-hidden="true"
        >
          <path d="m15 5-7 7 7 7" stroke-linecap="round" stroke-linejoin="round" />
        </svg>
      </button>
      <p class="title" aria-live="polite">{title}</p>
      <button
        type="button"
        class="page"
        aria-label={m['plan.jump.nextMonth']()}
        onclick={() => page(1)}
      >
        <svg
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="2"
          aria-hidden="true"
        >
          <path d="m9 5 7 7-7 7" stroke-linecap="round" stroke-linejoin="round" />
        </svg>
      </button>
    </div>

    <div class="grid">
      {#each weekdays as weekday, index (index)}
        <span class="weekday" aria-hidden="true">{weekday}</span>
      {/each}
      {#each days as day (day)}
        <button
          type="button"
          class="day"
          class:outside={day.slice(0, 7) !== month.slice(0, 7)}
          class:shown={day >= from && day <= to}
          class:today={day === today}
          aria-current={day === today ? 'date' : undefined}
          aria-label={new Intl.DateTimeFormat(locale, { dateStyle: 'full' }).format(dayOf(day))}
          onclick={(event) => pick(event, day)}
        >
          {Number(day.slice(-2))}
        </button>
      {/each}
    </div>
  </div>
</Popover>

<style>
  /* The range reads as text and opens the month: a quiet control, no capsule. */
  .trigger {
    display: inline-flex;
    align-items: center;
    gap: var(--space-1);
    min-height: var(--control-sm);
    margin-inline-start: calc(-1 * var(--space-2));
    padding-inline: var(--space-2);
    border: none;
    border-radius: var(--radius-full);
    background: none;
    color: var(--text-muted);
    font: inherit;
    font-variant-numeric: tabular-nums;
    cursor: pointer;
  }

  .trigger:hover {
    background: var(--surface-hover);
    color: var(--text);
  }

  .trigger:focus-visible,
  .page:focus-visible,
  .day:focus-visible {
    outline: 2px solid var(--border-focus);
    outline-offset: 2px;
  }

  .trigger svg {
    width: 1rem;
    height: 1rem;
  }

  .month {
    width: 18.5rem;
    max-width: 100%;
    padding: var(--space-1);
  }

  .head {
    display: flex;
    align-items: center;
    justify-content: space-between;
    margin-bottom: var(--space-2);
  }

  .title {
    margin: 0;
    font-weight: var(--weight-semibold);
  }

  .page,
  .day {
    display: inline-grid;
    place-items: center;
    padding: 0;
    border: none;
    border-radius: var(--radius-full);
    background: none;
    color: var(--text);
    font: inherit;
    cursor: pointer;
  }

  .page {
    width: var(--control-sm);
    height: var(--control-sm);
  }

  .page svg {
    width: 1.125rem;
    height: 1.125rem;
  }

  .page:hover,
  .day:hover {
    background: var(--surface-hover);
  }

  .grid {
    display: grid;
    grid-template-columns: repeat(7, minmax(0, 1fr));
    row-gap: var(--space-1);
  }

  .weekday {
    padding-block: var(--space-1);
    color: var(--text-subtle);
    font-size: var(--text-xs);
    font-weight: var(--weight-medium);
    text-align: center;
  }

  /* 40px cells in a 296px panel: under the 44px floor by a hair, as in every calendar's month, and never crowded. */
  .day {
    justify-self: center;
    width: 2.5rem;
    height: 2.5rem;
    font-size: var(--text-sm);
    font-variant-numeric: tabular-nums;
  }

  .outside {
    color: var(--text-subtle);
  }

  .shown {
    background: var(--surface-accent-subtle);
  }

  .today {
    background: var(--accent);
    color: var(--accent-contrast);
    font-weight: var(--weight-semibold);
  }

  .today:hover {
    background: var(--accent);
  }
</style>
