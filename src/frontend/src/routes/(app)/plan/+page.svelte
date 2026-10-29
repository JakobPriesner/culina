<script lang="ts">
  import { onDestroy } from 'svelte';
  import { MediaQuery } from 'svelte/reactivity';

  import { page } from '$app/state';
  import { Button, ErrorState } from '$ds';

  import CarriedMeal from '$features/planning/CarriedMeal.svelte';
  import CoachTip from '$features/onboarding/CoachTip.svelte';
  import OlliSays from '$features/onboarding/OlliSays.svelte';
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
  import { explain } from '$shell/explain';
  import { m } from '$shell/i18n';
  import Page from '$shell/Page.svelte';
  import { preferences } from '$shell/preferences.svelte';
  import { toaster } from '$shell/toaster.svelte';
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
   * Too narrow for back, today and forward beside Plan | Liste (a phone upright): a second pinned row would take a
   * quarter of the screen, so there they sit beside the dates and scroll away. The bar holds them beside a whole
   * Plan | Liste, the size it is on the list, from about 34.5rem.
   */
  const narrow = new MediaQuery('(width < 35rem)', false);

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
  const foldsEarlier = $derived(
    view === 'week' && start === mondayOf(0) && !planView.earlierShown(start)
  );

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
  // On a narrow phone the range shares its row with back, today and forward: the dates alone, no weekdays.
  const range = $derived(rangeOf(start, preferences.locale, count, narrow.current));

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

      return;
    }

    const failure = mealPlan.writeError;
    toaster.show({
      message: () => (failure ? explain(failure) : m['plan.recipe.failed']()),
      tone: 'danger'
    });
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

{#snippet diaryToday()}<DiaryTodayLine />{/snippet}

{#snippet tip()}
  <!-- Moving meals means nothing before there is one, and the empty week has its own Olli. -->
  {#if mealPlan.meals.length > 0}
    <CoachTip id="plan" pose="thinking" title={m['tips.plan.title']()}>
      <p>{m['tips.plan.drag']()}</p>
      <p>{m['tips.plan.shop']()}</p>
    </CoachTip>
  {/if}
{/snippet}
<svelte:head><title>{title}</title></svelte:head>

{#snippet travel()}<PlanNav {view} {atToday} onstep={step} ontoday={toToday} />{/snippet}

<Page>
  <!-- On a phone the way through time rides in the header bar beside Plan | Liste, so it is always in reach. -->
  <WocheSwitch view="plan" trailing={travelInBar ? travel : undefined} />
  <div class="hub" class:beside={listBeside.current}>
    <!-- Beside the list each column scrolls itself, so dragging a meal past an edge scrolls this one, not the page. -->
    <div class="week" data-plan-scroller={listBeside.current || undefined}>
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
          onunfold={() => planView.showEarlier(start)}
          onlines={listBeside.current ? (meal) => lineMarks.show(meal.entryId) : undefined}
          ontolist={listBeside.current ? (meal) => void actions.toList(meal.entryId) : undefined}
          ondrop={actions.drop}
          onadd={addOn}
          onedit={(meal) => (editing = meal)}
          onremove={(meal) => void actions.unplan(meal)}
          onmadeit={(meal) => void actions.madeIt(meal)}
          todayExtra={householdId ? diaryToday : undefined}
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
            <!-- Olli under the days and talking about them, in line with them: centred in the column, a page-sized
                 empty state floated below the rows it was about, often below the fold. -->
            <div class="empty-week" class:wide={wide.current}>
              <OlliSays pose="peeking" size={wide.current ? 'md' : 'sm'}>
                <h2 class="empty-title">{m['plan.empty.title']()}</h2>
                <p>{m['plan.empty.description']()}</p>
                <div class="empty-action">
                  <Button variant="primary" onclick={() => addOn(firstOpenDay)}
                    >{m['plan.empty.action']()}</Button
                  >
                </div>
              </OlliSays>
            </div>
          {:else}
            <p class="nothing-planned">
              {m['plan.empty.title']()}
              <button
                type="button"
                class="text-action hit-target reach"
                onclick={() => addOn(firstOpenDay)}>{m['plan.empty.action']()}</button
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
  /*
   * The week and, from 80rem, the list beside it, parted by one straight hairline the height of the pane. The two fill
   * the screen below the header and each scrolls itself: the page does not scroll as well (a short window still lets it,
   * rather than leave two slivers).
   */
  .beside {
    display: grid;
    grid-template-columns: minmax(0, 1fr) minmax(18rem, 24rem);
    grid-template-rows: minmax(0, 1fr);
    gap: var(--space-8);
    height: calc(100dvh - var(--pinned-top) - var(--bottom-inset) - var(--space-6));
    min-height: 24rem;
  }

  /* The page's room to scroll past the end, kept only as the gap under the columns. */
  :global(.page):has(> .beside) {
    padding-block-end: var(--space-6);
  }

  .beside > .week,
  .list {
    overflow-y: auto;
    overscroll-behavior: contain;
  }

  /* Each column scrolls itself, so a day sought and a sticky heading go to the column's top, not under the shell's header. */
  .beside > .week,
  .list > :global(*) {
    --header-inset: 0px;
  }

  .tip-after {
    margin-top: var(--space-6);
  }

  .empty-week {
    margin-top: var(--space-6);
  }

  /* Olli at the bubble's top, as in a coach tip: the button makes the bubble taller than the pot. */
  .empty-week :global(.says) {
    align-items: flex-start;
  }

  .empty-week :global(.bubble) {
    max-width: 34rem;
  }

  /* The tail at the pot's middle: the small one on a phone, so the bubble keeps the width to read in. */
  .empty-week :global(.bubble::before) {
    top: 1.4rem;
  }

  .empty-week.wide :global(.bubble::before) {
    top: 2.4rem;
  }

  .empty-title {
    margin-bottom: var(--space-1);
    font-family: var(--font-editorial);
    font-size: var(--text-xl);
    font-weight: var(--weight-regular);
    letter-spacing: -0.02em;
  }

  .empty-week p {
    color: var(--text-muted);
    text-wrap: pretty;
  }

  .empty-action {
    margin-top: var(--space-3);
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
    padding-inline-start: var(--space-8);
    border-inline-start: 1px solid var(--border);
  }

  /* A meal carried over the list: dropping it here puts its shopping on, as a day's outline says it would land there. */
  .list.over {
    outline: 2px dashed var(--border-focus);
    outline-offset: calc(-1 * var(--space-1));
  }
</style>
