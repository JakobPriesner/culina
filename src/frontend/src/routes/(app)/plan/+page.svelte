<script lang="ts">
  import { onDestroy } from 'svelte';
  import { MediaQuery } from 'svelte/reactivity';

  import { page } from '$app/state';
  import { Button, EmptyState, ErrorState } from '$ds';

  import CarriedMeal from '$features/planning/CarriedMeal.svelte';
  import CoachTip from '$features/onboarding/CoachTip.svelte';
  import {
    asDate,
    mealPlan,
    placeOf,
    plusDays,
    weekStart,
    type MealSlot,
    type PlannedMeal
  } from '$features/planning/mealPlan.svelte';
  import { shopping } from '$features/shopping/stores/shopping.svelte';
  import EditMealSheet from '$features/planning/EditMealSheet.svelte';
  import PlanHeader from '$features/planning/PlanHeader.svelte';
  import PlanRecipePicker from '$features/planning/PlanRecipePicker.svelte';
  import PlanShopBar from '$features/planning/PlanShopBar.svelte';
  import PlanWeek from '$features/planning/PlanWeek.svelte';
  import WeekToList from '$features/planning/WeekToList.svelte';
  import PlanWeekSkeleton from '$features/planning/PlanWeekSkeleton.svelte';
  import { usePlanActions } from '$features/planning/usePlanActions.svelte';
  import {
    daysTitle,
    mondayOf,
    rangeOf,
    weekOffsetOf,
    weekTitle
  } from '$features/planning/weekDates';
  import PlanNav from '$features/planning/PlanNav.svelte';
  import { daysShown, planView, type PlanView } from '$features/planning/planView.svelte';
  import { lineMarks } from '$features/shopping/lineMarks.svelte';
  import { WeekDrag } from '$features/planning/weekDrag.svelte';
  import { recipeAnswers } from '$features/nutrition/stores/recipeAnswers.svelte';
  import type { RecipeSummary } from '$features/recipes/types';
  import { session } from '$features/auth/session.svelte';
  import { m } from '$shell/i18n';
  import Olli from '$shell/olli/Olli.svelte';
  import Page from '$shell/Page.svelte';
  import { preferences } from '$shell/preferences.svelte';
  import DiaryTodayLine from '$features/diary/DiaryTodayLine.svelte';
  import WocheSwitch from '$shell/WocheSwitch.svelte';
  import ShoppingListView from '$features/shopping/ShoppingListView.svelte';
  import WithdrawNote from '$features/shopping/WithdrawNote.svelte';

  /**
   * The plan, looked at as the coming days or as a week (docs/woche-design.md §10); never a calendar to plan a month in
   * (that brings recurrence and a second shopping list), though the month is how to jump to a date. Meals move by dragging.
   * Woche's first view: the list is its second (docs/woche-design.md).
   */
  const householdId = $derived(session.activeHouseholdId);

  /**
   * From 80rem the list sits beside the week, live; below, the week needs the whole width (seven day columns broke
   * words at 1280px beside a pane), so the switch stays.
   */
  const listBeside = new MediaQuery('(min-width: 80rem)', false);

  /**
   * Too narrow for back, today and forward beside Plan | Liste (320px): a second pinned row would take a quarter of
   * the screen, so there they sit by the title and scroll away.
   */
  const narrow = new MediaQuery('(width < 23rem)', false);

  /** Where back, today and forward go: the header bar, unless the list is beside the week or the phone is narrow. */
  const travelInBar = $derived(!listBeside.current && !narrow.current);

  /** Wide enough for a day to be a row: its name in a column, its meals side by side. */
  const wide = new MediaQuery('(min-width: 48rem)', false);

  const today = asDate(new Date());

  /** `?week=1` opens on next week, as "See the week" does after planning ahead; `?tag=` on a day a list line leads back to. */
  const askedWeek = Number.parseInt(page.url.searchParams.get('week') ?? '', 10);

  /**
   * The coming days on a phone, where the week's first days would be a screen of meals already eaten; the week beside
   * the list, which it is the shopping of. Whichever was chosen last on this device wins over both.
   */
  const opening: PlanView = Number.isNaN(askedWeek)
    ? (planView.chosen ?? (listBeside.current ? 'week' : 'days'))
    : 'week';

  let view = $state(opening);

  /** The first day on screen: a Monday in the week view, any day in the days view. */
  let start = $state(
    Number.isNaN(askedWeek) ? (opening === 'week' ? mondayOf(0) : today) : mondayOf(askedWeek)
  );

  const count = $derived(view === 'week' ? 7 : daysShown);
  const end = $derived(plusDays(start, count - 1));
  const offset = $derived(weekOffsetOf(start));
  const atToday = $derived(view === 'week' ? start === mondayOf(0) : start === today);

  /** `?tag=`: the day a list line (or a meal on the list) leads back to, shown and marked. */
  let sought = $state<string | null>(null);

  // Also for a link followed on this page, from the list beside the week.
  $effect(() => {
    const tag = page.url.searchParams.get('tag');

    if (tag) {
      sought = tag;
      start = view === 'week' ? weekStart(tag) : tag;
    }
  });

  /** The days of this week already over, folded into one line until asked for: on a Saturday, Monday is history. */
  let showEarlier = $state(false);
  const foldsEarlier = $derived(view === 'week' && start === mondayOf(0) && !showEarlier);

  function choose(next: PlanView) {
    planView.choose(next);
    // The week holding what was shown; the days from today when that week is this one, else from its Monday.
    start = next === 'week' ? weekStart(start) : weekStart(start) === mondayOf(0) ? today : start;
    view = next;
  }

  function step(direction: -1 | 1) {
    start = plusDays(start, direction * (view === 'week' ? 7 : 1));
  }

  function toToday() {
    start = view === 'week' ? mondayOf(0) : today;
  }

  function jump(date: string) {
    start = view === 'week' ? weekStart(date) : date;
  }

  /** The title says what is shown: this week, next week, the coming days, or from which day. */
  const title = $derived(
    view === 'week' ? weekTitle(offset) : daysTitle(start, preferences.locale)
  );
  const range = $derived(rangeOf(start, preferences.locale, count));

  /** The day an "add" was pressed for, which is also what opens the picker. */
  let adding = $state<string | null>(null);
  let slot = $state<MealSlot>('dinner');

  /** The meal being answered in the sheet (its day, meal and servings), rather than aimed at. */
  let editing = $state<PlannedMeal | null>(null);

  /** Where "Rezept planen" plans for: today when it is on screen, else the first day shown. */
  const firstOpenDay = $derived(start <= today && today <= end ? today : start);

  const plannedThisWeek = $derived([
    ...new Set(mealPlan.days.flatMap((day) => day.meals.map((meal) => meal.recipeId)))
  ]);

  const unshopped = $derived(
    mealPlan.weeks.reduce((sum, monday) => sum + mealPlan.unshoppedIn(monday).length, 0)
  );

  const drag = new WeekDrag();
  const actions = usePlanActions({
    householdId: () => householdId,
    reload: () => (householdId ? mealPlan.refresh(householdId) : Promise.resolve())
  });

  onDestroy(() => {
    drag.stop();
    // The next visit asks again (a 304 when nothing changed), so an edit or a correction made meanwhile shows.
    recipeAnswers.reset();
  });

  $effect(() => {
    if (householdId) {
      void mealPlan.load(householdId, start, count);
    }
  });

  // Each planned recipe's nutrition, asked for in parallel and never waited for: the day lines appear as they arrive.
  $effect(() => {
    if (householdId) {
      void recipeAnswers.ensure(plannedThisWeek, householdId);
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

  function addOn(date: string) {
    adding = date;
    slot = 'dinner';
  }

  function sheetAnswered(to: {
    date: string;
    slot: MealSlot;
    servings: number;
    updateList: boolean;
  }) {
    const meal = editing;

    editing = null;

    if (meal) {
      void actions.edit(meal, to);
    }
  }
</script>

{#snippet peeking()}<Olli pose="peeking" />{/snippet}
{#snippet tip()}
  <CoachTip id="plan" pose="thinking" title={m['tips.plan.title']()}>
    <p>{m['tips.plan.drag']()}</p>
    <p>{m['tips.plan.shop']()}</p>
  </CoachTip>
{/snippet}
<svelte:head><title>{title}</title></svelte:head>

{#snippet travel()}<PlanNav {view} {atToday} onstep={step} ontoday={toToday} />{/snippet}

<Page>
  <!-- On a phone the way through time rides in the header bar beside Plan | Liste, so it is always in reach. -->
  <WocheSwitch view="plan" trailing={travelInBar ? travel : undefined} />
  <div class="hub" class:beside={listBeside.current}>
    <div class="week">
      <PlanHeader
        {title}
        {range}
        from={start}
        to={end}
        locale={preferences.locale}
        {view}
        onview={choose}
        nav={!travelInBar}
        {atToday}
        onstep={step}
        ontoday={toToday}
        onjump={jump}
      />

      <!-- Today is the first day shown whenever it is on screen (the days gone fold away), and its meals carry
           "Kochen" themselves: a Today card above them would say it twice. -->
      {#if householdId && start <= today && today <= end}<DiaryTodayLine />{/if}

      <!-- Above the week on a phone, where it is read first; beside the list under it, so the rows keep the screen. -->
      {#if !listBeside.current}{@render tip()}{/if}

      {#if mealPlan.error}
        <ErrorState title={m['error.unexpected.title']()} body={m['error.unexpected.body']()}>
          {#snippet action()}
            <Button
              variant="primary"
              onclick={() => householdId && void mealPlan.load(householdId, start, count)}
            >
              {m['error.retry']()}
            </Button>
          {/snippet}
        </ErrorState>
      {:else if mealPlan.loading && mealPlan.days.length === 0}
        <PlanWeekSkeleton rows={wide.current} {count} />
      {:else}
        <PlanWeek
          days={mealPlan.days}
          {householdId}
          {drag}
          rows={wide.current}
          seek={sought}
          folded={foldsEarlier}
          onunfold={() => (showEarlier = true)}
          onlines={listBeside.current ? (meal) => lineMarks.show(meal.entryId) : undefined}
          ontolist={listBeside.current ? (meal) => void actions.toList(meal.entryId) : undefined}
          ondrop={actions.drop}
          onadd={addOn}
          onedit={(meal) => (editing = meal)}
          onremove={(meal) => void actions.unplan(meal)}
          onmadeit={(meal) => void actions.madeIt(meal)}
        />

        {#if mealPlan.meals.length > 0}
          <!-- Beside the list the seam at its top does this, where the lines arrive. -->
          {#if !listBeside.current}
            <PlanShopBar {unshopped} busy={actions.ui.busy} onshop={() => void actions.shop()} />
          {/if}
        {:else if !mealPlan.loading}
          <!-- One state for the place: the big one only while the list is empty too (the pane then shows just its
               add field); a list in use gets a quiet line, as nothing about it is missing. -->
          {#if shopping.status === 'loading' || shopping.status === 'idle'}
            <!-- Which of the two is not known before the list is read. -->
          {:else if shopping.items.length === 0}
            <EmptyState
              title={m['plan.empty.title']()}
              body={m['plan.empty.description']()}
              art={peeking}
            >
              {#snippet action()}
                <Button variant="primary" onclick={() => addOn(firstOpenDay)}
                  >{m['plan.empty.action']()}</Button
                >
              {/snippet}
            </EmptyState>
          {:else}
            <p class="nothing-planned">
              {m['plan.empty.title']()}
              <Button size="sm" variant="ghost" onclick={() => addOn(firstOpenDay)}
                >{m['plan.empty.action']()}</Button
              >
            </p>
          {/if}
        {/if}
      {/if}

      {#if listBeside.current}<div class="tip-after">{@render tip()}</div>{/if}
    </div>
    {#if listBeside.current}
      <!-- A meal dropped here puts its shopping on the list; the meal stays on its day. -->
      <aside
        class="list"
        class:over={drag.landing?.list === true}
        aria-label={m['shopping.title']()}
        data-plan-list
      >
        <ShoppingListView pane shown={{ from: start, to: end }}>
          {#snippet seam()}
            {#if householdId}
              <WithdrawNote {householdId} />
              {#each mealPlan.weeks as monday (monday)}
                <WeekToList {householdId} week={monday} offset={weekOffsetOf(monday)} />
              {/each}
            {/if}
          {/snippet}
        </ShoppingListView>
      </aside>
    {/if}
  </div>
</Page>

{#if drag.held}
  <CarriedMeal held={drag.held} at={drag.at} />
{/if}

<EditMealSheet
  meal={editing}
  days={mealPlan.aroundDays}
  from={editing ? (placeOf(mealPlan.knownDays, editing.entryId)?.date ?? start) : start}
  onsave={sheetAnswered}
  onclose={() => (editing = null)}
/>

{#if householdId}
  <PlanRecipePicker
    open={adding !== null}
    {householdId}
    date={adding}
    days={mealPlan.aroundDays}
    bind:slot
    onpick={(recipe) => void pick(recipe)}
    onclose={() => (adding = null)}
  />
{/if}

<style>
  /* The week and, from 80rem, the list beside it, parted by one straight hairline the height of the pane. */
  .beside {
    display: grid;
    grid-template-columns: minmax(0, 1fr) minmax(18rem, 24rem);
    gap: var(--space-8);
    align-items: start;
  }

  .tip-after {
    margin-top: var(--space-6);
  }

  .nothing-planned {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-2);
    margin-top: var(--space-4);
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .list {
    position: sticky;
    top: var(--header-inset, var(--space-16));
    max-height: calc(100dvh - var(--header-inset, var(--space-16)) - var(--space-6));
    overflow-y: auto;
    padding-inline-start: var(--space-8);
    border-inline-start: 1px solid var(--border);
  }

  /* A meal carried over the list: dropping it here puts its shopping on, as a day's outline says it would land there. */
  .list.over {
    outline: 2px dashed var(--border-focus);
    outline-offset: calc(-1 * var(--space-1));
  }
</style>
