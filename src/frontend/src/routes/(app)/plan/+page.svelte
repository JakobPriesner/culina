<script lang="ts">
  import { onDestroy } from 'svelte';

  import { resolve } from '$app/paths';
  import { Button, EmptyState, ErrorState } from '$ds';

  import PlannedCard from '$features/planning/PlannedCard.svelte';
  import MoveMealSheet from '$features/planning/MoveMealSheet.svelte';
  import {
    asDate,
    gapToRestore,
    mealPlan,
    placeOf,
    type MealSlot,
    type PlannedMeal
  } from '$features/planning/mealPlan.svelte';
  import { WeekDrag, type Held, type Landing } from '$features/planning/weekDrag.svelte';
  import { cookbooks } from '$features/cookbooks/stores/cookbooks.svelte';
  import RecipePicker from '$features/recipes/RecipePicker.svelte';
  import type { RecipeSummary } from '$features/recipes/types';
  import { session } from '$features/auth/session.svelte';
  import { shopping } from '$features/shopping/stores/shopping.svelte';
  import { toaster } from '$shell/toaster.svelte';
  import { m } from '$shell/i18n';
  import Page from '$shell/Page.svelte';
  import { preferences } from '$shell/preferences.svelte';

  /**
   * What this household means to cook this week.
   *
   * Seven days, and deliberately not a calendar. A week is the unit people
   * actually plan in — you shop at the weekend for the week that follows — and
   * a month view is where recurrence and a second shopping list come from.
   *
   * Meals are moved between the days by dragging them, which is the commonest
   * edit a plan gets: a week is agreed on Sunday and then argued with all week.
   *
   * It is reached from the recipe list rather than from the navigation bar,
   * which is closed at three on purpose: a plan is a weekly thing, and anything
   * rarer than daily belongs behind one of the three rather than beside it.
   */
  const householdId = $derived(session.activeHouseholdId);

  /** A new Date, never an adjusted one. */
  const addDays = (from: Date, days: number) =>
    new Date(from.getFullYear(), from.getMonth(), from.getDate() + days);

  /** Midday, so a time zone west of UTC cannot move a date to the day before. */
  const dayOf = (date: string) => new Date(`${date}T12:00:00`);

  /** Which Monday is on screen, as an offset in weeks from this one. */
  let offset = $state(0);

  /** The day an "add" was pressed for, which is also what opens the picker. */
  let adding = $state<string | null>(null);
  let slot = $state<MealSlot>('dinner');

  /** Which shelf the picker is searching, or null for everything. */
  let narrowedTo = $state<string | null>(null);
  let busy = $state(false);

  /** The meal whose move is being answered in the sheet, rather than aimed at. */
  let moving = $state<PlannedMeal | null>(null);

  const drag = new WeekDrag();

  onDestroy(() => drag.stop());

  const monday = $derived.by(() => {
    const now = new Date();

    // Built by arithmetic rather than by mutating a Date: a date that is
    // adjusted in place is a date somebody else is holding a reference to.
    return asDate(addDays(now, -((now.getDay() + 6) % 7) + offset * 7));
  });

  /**
   * The week, worded.
   *
   * `formatRange` rather than two formatted dates with a dash between them: it
   * knows not to say the month twice, and it knows which side of the number the
   * month goes on. "September 7 – 13", not "September 7 – September 13".
   */
  const heading = $derived.by(() => {
    const start = dayOf(monday);

    return new Intl.DateTimeFormat(preferences.locale, {
      day: 'numeric',
      month: 'long'
    }).formatRange(start, addDays(start, 6));
  });

  /**
   * The weekday and the number, formatted apart and stacked.
   *
   * Asking Intl for both at once hands back a different word order per locale —
   * and for bare "en" an order that reads oddly in a column heading. Two lines
   * fit a narrow column better anyway, and the month is already in the heading
   * above, so a bare number is not ambiguous.
   */
  const weekdays = $derived(new Intl.DateTimeFormat(preferences.locale, { weekday: 'long' }));

  const today = asDate(new Date());

  $effect(() => {
    if (householdId) {
      void mealPlan.load(householdId, monday);
    }
  });

  // The shelves, so the picker can offer to narrow to one. Only worth asking
  // for once the picker can be opened, which is whenever this page is.
  $effect(() => {
    if (householdId) {
      void cookbooks.list(householdId);
    }
  });

  async function pick(recipe: RecipeSummary) {
    if (!householdId || !adding) {
      return;
    }

    const ok = await mealPlan.plan(householdId, { date: adding, recipeId: recipe.id, slot });

    if (ok) {
      adding = null;
    }
  }

  /**
   * Puts a meal on another day, and offers to put it back.
   *
   * Undo rather than a confirmation: a drop is cheap to reverse and expensive
   * to interrupt, and the drop that lands a day out is common enough on a phone
   * that the way back has to be on the screen it lands on.
   */
  async function move(entryId: string, to: { date: string; slot?: MealSlot; position?: number }) {
    const before = householdId && placeOf(mealPlan.days, entryId);

    if (!householdId || !before) {
      return;
    }

    const ok = await mealPlan.move(householdId, entryId, to);

    if (!ok) {
      toaster.show({ message: m['plan.move.failed'](), tone: 'danger' });

      return;
    }

    // Where it landed, read from the week that came back rather than from the
    // one that was on screen: the server decides the order of a day, so its
    // answer is the only one an undo can be built against.
    const after = placeOf(mealPlan.days, entryId);

    toaster.show({
      message: m['plan.move.done']({ day: weekdays.format(dayOf(to.date)) }),
      action: after
        ? {
            label: m['plan.move.undo'](),
            run: () =>
              void move(entryId, {
                date: before.date,
                slot: before.slot,
                position: gapToRestore(before, after)
              })
          }
        : undefined
    });
  }

  /** A card let go over a day. The gaps either side of where it was are no move. */
  function drop(held: Held, landing: Landing) {
    const before = placeOf(mealPlan.days, held.entryId);

    if (
      before?.date === landing.date &&
      (landing.position === before.index || landing.position === before.index + 1)
    ) {
      return;
    }

    void move(held.entryId, { date: landing.date, position: landing.position });
  }

  /**
   * Puts the week's shopping on the list, one recipe at a time.
   *
   * The same call the recipe page makes, repeated. A second code path that
   * merged a whole week at once would be a second place for merging to be
   * subtly different, and merging is the entire value of the list.
   */
  async function shop() {
    if (!householdId) {
      return;
    }

    busy = true;

    for (const meal of mealPlan.meals) {
      await shopping.addRecipe(householdId, meal.recipeId, meal.servings ?? meal.recipeServings);
    }

    busy = false;

    toaster.show({
      message: m['plan.addedToList']({ count: mealPlan.meals.length }),
      tone: 'success',
      action: {
        label: m['plan.openList'](),
        run: () => void goToList()
      }
    });
  }

  const goToList = async () => {
    const { goto } = await import('$app/navigation');

    await goto(resolve('/(app)/shopping'));
  };
</script>

<svelte:head><title>{m['plan.title']()}</title></svelte:head>

<Page>
  <header class="head">
    <div class="heading">
      <h1 class="title">{m['plan.title']()}</h1>
      <p class="range">{heading}</p>
    </div>

    <div class="weeks">
      <Button size="sm" onclick={() => (offset -= 1)}>← {m['plan.previous']()}</Button>
      {#if offset !== 0}
        <Button size="sm" onclick={() => (offset = 0)}>{m['plan.thisWeek']()}</Button>
      {/if}
      <Button size="sm" onclick={() => (offset += 1)}>{m['plan.next']()} →</Button>
    </div>
  </header>

  {#if mealPlan.error}
    <ErrorState title={m['error.unexpected.title']()} body={m['error.unexpected.body']()}>
      {#snippet action()}
        <Button
          variant="primary"
          onclick={() => householdId && void mealPlan.load(householdId, monday)}
        >
          {m['error.retry']()}
        </Button>
      {/snippet}
    </ErrorState>
  {:else}
    <ol class="week" class:dragging={drag.held !== null}>
      {#each mealPlan.days as day (day.date)}
        <li
          class="day"
          class:today={day.date === today}
          class:over={drag.landing?.date === day.date}
          data-plan-day={day.date}
        >
          <h2 class="name">
            {weekdays.format(dayOf(day.date))}
            <span class="number">{Number(day.date.slice(-2))}</span>
          </h2>

          {#if day.meals.length > 0 || drag.landing?.date === day.date}
            <ul class="meals">
              {#each day.meals as meal, index (meal.entryId)}
                <!-- The line the card would land on. Drawn between the cards
                     rather than around the day, because a day is the answer to
                     "which day" and this is the answer to "where in it". -->
                {#if drag.landing?.date === day.date && drag.landing.position === index}
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
                        drop
                      )}
                    onmove={() => (moving = meal)}
                    onremove={() => householdId && void mealPlan.unplan(householdId, meal.entryId)}
                  />
                </li>
              {/each}

              {#if drag.landing?.date === day.date && drag.landing.position >= day.meals.length}
                <li class="seam" aria-hidden="true"></li>
              {/if}
            </ul>
          {/if}

          <!-- Every day offers it, planned or not. An "add" that only appears
               on an empty day is an add you cannot use twice. -->
          <Button
            size="sm"
            variant="ghost"
            onclick={() => {
              adding = day.date;
              slot = 'dinner';
            }}
          >
            + {m['plan.add']()}
          </Button>
        </li>
      {/each}
    </ol>

    {#if mealPlan.meals.length > 0}
      <div class="shop">
        <Button variant="primary" loading={busy} onclick={() => void shop()}>
          {m['plan.toShoppingList']()}
        </Button>
      </div>
    {:else if !mealPlan.loading}
      <EmptyState title={m['plan.empty.title']()} body={m['plan.empty.description']()}>
        {#snippet action()}
          <Button variant="primary" href={resolve('/(app)')}>{m['plan.empty.action']()}</Button>
        {/snippet}
      </EmptyState>
    {/if}
  {/if}
</Page>

<!-- The card under the pointer. A copy rather than the card itself, so the day
     it came from keeps its shape and the gaps stay where they were aimed at. -->
{#if drag.held}
  <div
    class="carried"
    aria-hidden="true"
    style:inline-size="{drag.held.width}px"
    style:translate="{drag.at.x}px {drag.at.y}px"
  >
    <span class="carried-title">{drag.held.title}</span>
  </div>
{/if}

<MoveMealSheet
  meal={moving}
  days={mealPlan.days}
  from={moving ? (placeOf(mealPlan.days, moving.entryId)?.date ?? monday) : monday}
  onmove={(to) => {
    const entryId = moving?.entryId;

    moving = null;

    if (entryId) {
      void move(entryId, to);
    }
  }}
  onclose={() => (moving = null)}
/>

{#if householdId}
  <RecipePicker
    open={adding !== null}
    {householdId}
    title={m['plan.pick.title']()}
    cookbookId={narrowedTo ?? undefined}
    onpick={(recipe) => void pick(recipe)}
    onclose={() => (adding = null)}
  >
    <!-- Which meal, and only here. A slot picker on the week view would put
         three empty rows on every day for the household that only plans
         dinner, which is most of them. -->
    {#snippet controls()}
      <!-- Narrowing to a shelf, and only when the household has one. "What are
           we cooking Thursday" is usually asked of a subset somebody has
           already chosen, and this is that subset. -->
      {#if cookbooks.items.length > 0}
        <fieldset class="slots">
          <legend>{m['cookbooks.title']()}</legend>
          <label>
            <input type="radio" name="cookbook" value={null} bind:group={narrowedTo} />
            {m['cookbooks.picker.all']()}
          </label>
          {#each cookbooks.items as cookbook (cookbook.id)}
            <label>
              <input type="radio" name="cookbook" value={cookbook.id} bind:group={narrowedTo} />
              {cookbook.name}
            </label>
          {/each}
        </fieldset>
      {/if}

      <fieldset class="slots">
        <legend>{m['plan.pick.slot']()}</legend>
        {#each ['breakfast', 'lunch', 'dinner'] as const as which (which)}
          <label>
            <input type="radio" name="slot" value={which} bind:group={slot} />
            {m[`plan.slot.${which}`]()}
          </label>
        {/each}
      </fieldset>
    {/snippet}
  </RecipePicker>
{/if}

<style>
  .head {
    display: flex;
    flex-wrap: wrap;
    align-items: baseline;
    justify-content: space-between;
    gap: var(--space-4);
    margin-bottom: var(--space-6);
  }

  .title {
    font-family: var(--font-editorial);
    font-size: var(--text-2xl);
    font-weight: var(--weight-regular);
  }

  .range {
    color: var(--text-muted);
  }

  .weeks {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2);
  }

  /* A long press is this list's own gesture, so the callout menu iOS would
     otherwise raise over a link is not wanted anywhere in it. */
  .week {
    -webkit-touch-callout: none;
  }

  /* Dragging across text selects it, on every desktop browser, and a week of
     highlighted recipe titles is the visible result of a drag that worked. */
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

  /* Under the pointer, and out of everything's way: it is not in the document
     flow, it does not take the pointer, and it is not in the accessibility
     tree — the card it was copied from is still all three of those. */
  .carried {
    position: fixed;
    top: 0;
    left: 0;
    z-index: var(--z-overlay);
    padding: var(--space-2);
    border-radius: var(--radius-sm);
    background: var(--surface-raised);
    box-shadow: var(--shadow-overlay);
    font-size: var(--text-sm);
    font-weight: var(--weight-medium);
    pointer-events: none;
    /* Held just above and left of the fingertip, so a thumb does not cover the
       thing it is carrying. */
    margin: calc(-1 * var(--space-6)) 0 0 calc(-1 * var(--space-4));
  }

  .carried-title {
    display: block;
    overflow: hidden;
    white-space: nowrap;
    text-overflow: ellipsis;
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

  .shop {
    margin-top: var(--space-6);
  }

  .slots {
    display: flex;
    flex-wrap: wrap;
    min-width: 0;
    gap: var(--space-4);
    padding: 0;
    border: 0;
  }

  .slots legend {
    color: var(--text-subtle);
    font-size: var(--text-xs);
    letter-spacing: 0.08em;
    text-transform: uppercase;
  }

  .slots label {
    min-height: var(--control-sm);
    display: flex;
    align-items: center;
    gap: var(--space-2);
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

    .name {
      flex-direction: column;
    }
  }
</style>
