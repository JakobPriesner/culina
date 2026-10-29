<script lang="ts">
  import { onDestroy, untrack } from 'svelte';
  import { MediaQuery } from 'svelte/reactivity';

  import { page } from '$app/state';
  import { Button, ErrorState } from '$ds';
  import { session } from '$features/auth/session.svelte';
  import DiaryTodayLine from '$features/diary/DiaryTodayLine.svelte';
  import { recipeAnswers } from '$features/nutrition/stores/recipeAnswers.svelte';
  import CoachTip from '$features/onboarding/CoachTip.svelte';
  import CarriedMeal from '$features/planning/CarriedMeal.svelte';
  import EditMealSheet from '$features/planning/EditMealSheet.svelte';
  import {
    mealPlan,
    placeOf,
    type MealSlot,
    type PlannedMeal
  } from '$features/planning/mealPlan.svelte';
  import PlanEmpty from '$features/planning/PlanEmpty.svelte';
  import PlanHeader from '$features/planning/PlanHeader.svelte';
  import PlanListPane from '$features/planning/PlanListPane.svelte';
  import PlanNav from '$features/planning/PlanNav.svelte';
  import PlanRecipePicker from '$features/planning/PlanRecipePicker.svelte';
  import PlanShopBar from '$features/planning/PlanShopBar.svelte';
  import PlanWeek from '$features/planning/PlanWeek.svelte';
  import PlanWeekSkeleton from '$features/planning/PlanWeekSkeleton.svelte';
  import { PlanWindow } from '$features/planning/planWindow.svelte';
  import { usePlanActions } from '$features/planning/usePlanActions.svelte';
  import { rangeOf } from '$features/planning/weekDates';
  import { WeekDrag } from '$features/planning/weekDrag.svelte';
  import type { RecipeSummary } from '$features/recipes/types';
  import { lineMarks } from '$features/shopping/lineMarks.svelte';
  import ListSummary from '$features/shopping/ListSummary.svelte';
  import { shopping } from '$features/shopping/stores/shopping.svelte';
  import { m } from '$shell/i18n';
  import { preferences } from '$shell/preferences.svelte';
  import { listBeside } from '$shell/woche.svelte';
  import WocheSwitch from '$shell/WocheSwitch.svelte';

  /**
   * The plan, looked at as the coming days or as a week (docs/woche-design.md §10); never a calendar to plan a month in
   * (that brings recurrence and a second shopping list), though the month is how to jump to a date. Meals move by dragging.
   * Woche's first view: the list is its second, and sits beside the week where it fits (`listBeside`).
   */
  const householdId = $derived(session.activeHouseholdId);

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

  const askedWeek = Number.parseInt(page.url.searchParams.get('week') ?? '', 10);
  const shown = new PlanWindow({
    week: Number.isNaN(askedWeek) ? null : askedWeek,
    listBeside: listBeside.current
  });

  // On a narrow phone the range shares its row with back, today and forward: the dates alone, no weekdays.
  const range = $derived(
    narrow.current ? rangeOf(shown.start, preferences.locale, shown.count, true) : shown.range
  );

  // `?tag=`, also for a link followed on this page from the list beside the week: only the address is followed.
  $effect(() => {
    const tag = page.url.searchParams.get('tag');

    if (tag) untrack(() => shown.seek(tag));
  });

  /** The day an "add" was pressed for, which is also what opens the picker. */
  let adding = $state<string | null>(null);
  let slot = $state<MealSlot>('dinner');

  /** The meal being answered in the sheet (its day, meal and servings), rather than aimed at. */
  let editing = $state<PlannedMeal | null>(null);

  const plannedRecipes = $derived([
    ...new Set(mealPlan.days.flatMap((day) => day.meals.map((meal) => meal.recipeId)))
  ]);

  const unshopped = $derived(
    mealPlan.weeks.reduce((sum, monday) => sum + mealPlan.unshoppedIn(monday).length, 0)
  );

  /** Whether the list is empty is not known before it is read, and an empty week says so differently for each. */
  const listRead = $derived(shopping.status !== 'idle' && shopping.status !== 'loading');

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

  function load() {
    if (householdId) void mealPlan.load(householdId, shown.start, shown.count);
  }

  $effect(load);

  // Each planned recipe's nutrition, asked for in parallel and never waited for: the day lines appear as they arrive.
  $effect(() => {
    if (householdId) void recipeAnswers.ensure(plannedRecipes, householdId);
  });

  function addOn(date: string) {
    adding = date;
    slot = 'dinner';
  }

  async function pick(recipe: RecipeSummary) {
    if (adding && (await actions.plan({ date: adding, recipeId: recipe.id, slot }))) {
      adding = null;
    }
  }

  function save(to: Parameters<typeof actions.edit>[1]) {
    const meal = editing;

    editing = null;

    if (meal) void actions.edit(meal, to);
  }
</script>

<svelte:head><title>{shown.title}</title></svelte:head>

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

{#snippet travel()}
  <PlanNav view={shown.view} atToday={shown.atToday} onstep={shown.step} ontoday={shown.toToday} />
{/snippet}

<!-- On a phone the way through time rides in the header bar beside Plan | Liste, so it is always in reach. -->
<WocheSwitch view="plan" trailing={travelInBar ? travel : undefined} />
<div class="hub" class:beside={listBeside.current}>
  <!-- Beside the list each column scrolls itself, so dragging a meal past an edge scrolls this one, not the page. -->
  <div class="week" data-plan-scroller={listBeside.current || undefined}>
    <PlanHeader
      title={shown.title}
      {range}
      from={shown.start}
      to={shown.end}
      locale={preferences.locale}
      view={shown.view}
      onview={shown.choose}
      nav={!travelInBar}
      atToday={shown.atToday}
      onstep={shown.step}
      ontoday={shown.toToday}
      onjump={shown.jump}
    />

    <!-- Above the week on a phone, where it is read first; beside the list under it, so the rows keep the screen. -->
    {#if !listBeside.current}{@render tip()}{/if}

    {#if mealPlan.error}
      <ErrorState title={m['error.unexpected.title']()} body={m['error.unexpected.body']()}>
        {#snippet action()}
          <Button variant="primary" onclick={load}>{m['error.retry']()}</Button>
        {/snippet}
      </ErrorState>
    {:else if mealPlan.loading && mealPlan.days.length === 0}
      <PlanWeekSkeleton rows={wide.current} count={shown.count} />
    {:else}
      <PlanWeek
        days={mealPlan.days}
        {householdId}
        {drag}
        rows={wide.current}
        seek={shown.sought}
        folded={shown.foldsEarlier}
        onunfold={shown.showEarlier}
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
      {:else if !mealPlan.loading && listRead}
        <PlanEmpty
          listInUse={shopping.items.length > 0}
          wide={wide.current}
          onplan={() => addOn(shown.firstOpenDay)}
        />
      {/if}
    {/if}

    <!-- Without the list beside it, the list at a glance under the week; the switch still leads to the whole list. -->
    {#if !listBeside.current && !mealPlan.error}<ListSummary />{/if}

    {#if listBeside.current}<div class="tip-after">{@render tip()}</div>{/if}
  </div>
  {#if listBeside.current}
    <PlanListPane
      {householdId}
      from={shown.start}
      to={shown.end}
      weeks={mealPlan.weeks}
      over={drag.landing?.list === true}
    />
  {/if}
</div>

{#if drag.held}
  <CarriedMeal held={drag.held} at={drag.at} />
{/if}

<EditMealSheet
  meal={editing}
  days={mealPlan.aroundDays}
  from={editing ? (placeOf(mealPlan.knownDays, editing.entryId)?.date ?? shown.start) : shown.start}
  onsave={save}
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
   * The week and, beside it, the list. The two fill the screen below the header and each scrolls itself: the page does
   * not scroll as well (a short window still lets it, rather than leave two slivers).
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

  /* Scrolled on its own, so a day sought and a sticky heading go to the column's top, not under the shell's header. */
  .beside > .week {
    overflow-y: auto;
    overscroll-behavior: contain;
    --header-inset: 0px;
  }

  .tip-after {
    margin-top: var(--space-6);
  }
</style>
