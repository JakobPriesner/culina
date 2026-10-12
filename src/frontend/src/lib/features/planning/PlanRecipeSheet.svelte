<script lang="ts">
  import { goto } from '$app/navigation';
  import { resolve } from '$app/paths';
  import { Button, SegmentedControl, Sheet, Skeleton } from '$ds';

  import { explain } from '$shell/explain';
  import { m } from '$shell/i18n';
  import { preferences } from '$shell/preferences.svelte';
  import { toaster } from '$shell/toaster.svelte';

  import { asDate, mealPlan, type MealSlot, type PlannedDay } from './mealPlan.svelte';
  import { mealSlots, slotLabel } from './slots';
  import { upcoming } from './upcoming.svelte';
  import { dayOf, mondayOf } from './weekDates';

  /**
   * Short path from choosing a recipe to the week: the days from today on (this week and the next,
   * so a Sunday still has a week ahead), and the meal; servings come from the detail page's scaling.
   */
  interface Props {
    open: boolean;
    householdId: string;
    recipeId: string;
    title: string;
    servings: number;
    onclose: () => void;
  }

  let { open, householdId, recipeId, title, servings, onclose }: Props = $props();

  let date = $state('');
  let slot = $state<MealSlot>('dinner');
  let saving = $state(false);

  const today = asDate(new Date());
  const days = $derived(upcoming.days);

  $effect(() => {
    if (!open) {
      return;
    }

    date = '';
    slot = 'dinner';
    void upcoming.load(householdId);
  });

  // Today, or the first day there is, until the person chooses; their answer stays while the plan reloads.
  $effect(() => {
    if (open && !days.some((day) => day.date === date)) {
      date = days[0]?.date ?? '';
    }
  });

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

  async function confirm() {
    if (!date || saving) {
      return;
    }

    saving = true;
    const ok = await mealPlan.plan(householdId, { date, recipeId, servings, slot });
    saving = false;

    // The recipe page and the library read what is coming up; tell them.
    void upcoming.load(householdId);

    if (!ok) {
      toaster.show({
        message: () => (mealPlan.error ? explain(mealPlan.error) : m['plan.recipe.failed']()),
        tone: 'danger'
      });

      return;
    }

    const day = longDay.format(dayOf(date));
    // The week page opens on this week; a day in the next one needs saying.
    const weekOf = date >= mondayOf(1) ? '?week=1' : '';

    toaster.show({
      message: () => m['plan.recipe.done']({ day }),
      tone: 'success',
      action: {
        label: () => m['plan.recipe.showWeek'](),
        // eslint-disable-next-line svelte/no-navigation-without-resolve -- The route is resolved; only the week query is appended.
        run: () => void goto(`${resolve('/(app)/plan')}${weekOf}`)
      }
    });
    onclose();
  }
</script>

<Sheet
  {open}
  title={m['plan.recipe.title']({ title })}
  closeLabel={m['plan.recipe.close']()}
  {onclose}
>
  {#if upcoming.error && days.length === 0}
    <p class="failure">{explain(upcoming.error)}</p>
  {:else if days.length === 0}
    <div class="loading" role="status" aria-busy="true" aria-label={m['plan.recipe.loading']()}>
      <Skeleton width="100%" height="4.5rem" />
      <Skeleton width="60%" height="1.25rem" />
      <Skeleton width="80%" height="2.75rem" />
    </div>
  {:else}
    <div class="answers">
      <fieldset>
        <legend>{m['plan.recipe.day']()}</legend>

        <!-- Native radios, so arrow keys move between days; each one's name says the whole date and what is planned on it. -->
        <div class="strip">
          {#each days as day (day.date)}
            <label class="day" class:today={day.date === today}>
              <input
                type="radio"
                name="plan-recipe-day"
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

      <div class="slot">
        <span class="caption">{m['plan.pick.slot']()}</span>
        <SegmentedControl
          {segments}
          selected={slot}
          label={m['plan.pick.slot']()}
          onselect={(id) => (slot = id as MealSlot)}
        />
      </div>
    </div>
  {/if}

  {#snippet footer()}
    <Button onclick={onclose}>{m['plan.recipe.cancel']()}</Button>
    <Button variant="primary" disabled={!date} loading={saving} onclick={confirm}>
      {m['plan.recipe.confirm']()}
    </Button>
  {/snippet}
</Sheet>

<style>
  .answers,
  .loading {
    display: flex;
    flex-direction: column;
    gap: var(--space-6);
  }

  fieldset {
    min-width: 0;
    padding: 0;
    border: 0;
  }

  legend,
  .caption {
    margin-bottom: var(--space-2);
    color: var(--text-muted);
    font-size: var(--text-sm);
    font-weight: var(--weight-semibold);
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

  .day:hover {
    background: var(--surface-hover);
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

  .slot {
    display: flex;
    flex-direction: column;
    align-items: flex-start;
  }

  .failure {
    color: var(--text-danger);
  }

  @media (prefers-reduced-motion: reduce) {
    .day {
      transition: none;
    }
  }
</style>
