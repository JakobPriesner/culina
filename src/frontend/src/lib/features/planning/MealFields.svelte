<script lang="ts">
  import type { Snippet } from 'svelte';

  import { SectionLabel, SegmentedControl, Stepper } from '$ds';

  import { m } from '$shell/i18n';
  import { preferences } from '$shell/preferences.svelte';

  import { dateToday, type MealSlot, type PlannedDay } from './mealPlan.svelte';
  import { mealSlots, slotLabel } from './slots';
  import { dayOf } from './weekDates';

  /**
   * Which day, which meal, for how many: the same three answers in the same order wherever a meal is
   * planned or changed, so planning from a recipe and editing on the week feel like one form.
   */
  interface Props {
    days: readonly PlannedDay[];
    date: string;
    slot: MealSlot;
    servings: number;
    /** The stepper was used: a figure it cannot show (2.5) stays until somebody does. */
    onservings?: () => void;
    /** Under the stepper: what the servings mean for this meal. */
    servingsHint?: string;
    /** After the servings: what else follows from the answer (the shopping list). */
    children?: Snippet;
  }

  let {
    days,
    date = $bindable(),
    slot = $bindable(),
    servings = $bindable(),
    onservings,
    servingsHint,
    children
  }: Props = $props();

  const id = $props.id();
  const today = dateToday();

  const longDay = $derived(
    new Intl.DateTimeFormat(preferences.locale, { weekday: 'long', day: 'numeric', month: 'long' })
  );
  const shortWeekday = $derived(new Intl.DateTimeFormat(preferences.locale, { weekday: 'short' }));
  const dayNumber = $derived(new Intl.DateTimeFormat(preferences.locale, { day: 'numeric' }));

  const plannedOn = (day: PlannedDay) =>
    day.meals.length > 0
      ? day.meals.map((meal) => meal.title).join(', ')
      : m['plan.move.freeDay']();

  const chosen = $derived(days.find((day) => day.date === date));

  const segments = $derived(mealSlots.map((which) => ({ id: which, label: slotLabel[which]() })));
</script>

<div class="answers">
  <fieldset>
    <legend
      ><SectionLabel tone="muted" as="span" spacing="tight">{m['plan.recipe.day']()}</SectionLabel
      ></legend
    >

    <!-- Native radios, so arrow keys move between days; each one's name says the whole date and what is planned on it. -->
    <div class="strip">
      {#each days as day (day.date)}
        <label class="day" class:today={day.date === today}>
          <input
            type="radio"
            name="{id}-day"
            value={day.date}
            bind:group={date}
            aria-label="{longDay.format(dayOf(day.date))}: {plannedOn(day)}"
          />
          <span class="weekday" aria-hidden="true">
            {day.date === today ? m['plan.today']() : shortWeekday.format(dayOf(day.date))}
          </span>
          <span class="number" aria-hidden="true">{dayNumber.format(dayOf(day.date))}</span>
          <span class="busy" class:planned={day.meals.length > 0} aria-hidden="true"></span>
        </label>
      {/each}
    </div>

    {#if chosen}
      <p class="planned" aria-hidden="true">
        <span class="chosen">{longDay.format(dayOf(chosen.date))}</span>
        {plannedOn(chosen)}
      </p>
    {/if}
  </fieldset>

  <div class="field">
    <SectionLabel tone="muted" as="span" spacing="tight">{m['plan.pick.slot']()}</SectionLabel>
    <SegmentedControl
      {segments}
      selected={slot}
      label={m['plan.pick.slot']()}
      onselect={(which) => (slot = which as MealSlot)}
    />
  </div>

  <div class="field">
    <SectionLabel tone="muted" as="span" spacing="tight">
      {m['recipe.servings.label']()}
    </SectionLabel>
    <Stepper
      id="{id}-servings"
      bind:value={servings}
      max={500}
      label={m['recipe.servings.label']()}
      decreaseLabel={m['recipe.servings.fewer']()}
      increaseLabel={m['recipe.servings.more']()}
      describedBy={servingsHint ? `${id}-servings-hint` : undefined}
      onchange={() => onservings?.()}
    />
    {#if servingsHint}
      <p class="hint" id="{id}-servings-hint">{servingsHint}</p>
    {/if}
  </div>

  {@render children?.()}
</div>

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

  /* One row of days that scrolls sideways, like a calendar's week strip; the edges bleed so a cut-off day says "there is more". */
  .strip {
    display: flex;
    gap: var(--space-2);
    margin-inline: calc(var(--space-1) * -1);
    padding: var(--space-1);
    overflow-x: auto;
    scroll-snap-type: x proximity;
    scrollbar-width: none;
  }

  .day {
    position: relative;
    display: flex;
    flex: none;
    flex-direction: column;
    align-items: center;
    gap: var(--space-1);
    min-width: 3.5rem;
    padding: var(--space-2) var(--space-1);
    border-radius: var(--radius-lg);
    background: var(--surface-sunken);
    color: var(--text);
    cursor: pointer;
    scroll-snap-align: start;
    transition:
      background-color var(--duration-fast) var(--ease-out),
      color var(--duration-fast) var(--ease-out);
  }

  @media (hover: hover) {
    .day:hover {
      background: var(--surface-hover);
    }
  }

  /* The input stays in the tab order and in the accessibility tree; the label is what you see. */
  .day input {
    position: absolute;
    inset: 0;
    margin: 0;
    opacity: 0;
    cursor: pointer;
  }

  .day:has(input:focus-visible) {
    outline: 2px solid var(--border-focus);
    outline-offset: 2px;
  }

  .weekday {
    color: var(--text-muted);
    font-size: var(--text-xs);
    font-weight: var(--weight-medium);
  }

  .today .weekday {
    color: var(--accent);
  }

  .number {
    font-size: var(--text-lg);
    font-weight: var(--weight-semibold);
    font-variant-numeric: tabular-nums;
    line-height: 1;
  }

  /* A dot for a day that already has a meal; empty space otherwise, so every day is the same height. */
  .busy {
    width: 0.375rem;
    height: 0.375rem;
    border-radius: var(--radius-full);
  }

  .busy.planned {
    background: var(--text-muted);
  }

  .day:has(input:checked) {
    background: var(--accent);
    color: var(--accent-contrast);
  }

  .day:has(input:checked) .weekday {
    color: var(--accent-contrast);
  }

  .day:has(input:checked) .busy.planned {
    background: var(--accent-contrast);
  }

  .planned {
    display: flex;
    flex-wrap: wrap;
    gap: 0 var(--space-2);
    margin-top: var(--space-3);
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .chosen {
    color: var(--text);
    font-weight: var(--weight-medium);
  }

  .field {
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

  @media (prefers-reduced-motion: reduce) {
    .day {
      transition: none;
    }
  }
</style>
