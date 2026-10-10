<script lang="ts">
  import { onDestroy } from 'svelte';

  import { resolve } from '$app/paths';
  import { Button, EmptyState, ErrorState } from '$ds';

  import CarriedMeal from '$features/planning/CarriedMeal.svelte';
  import {
    mealPlan,
    placeOf,
    type MealSlot,
    type PlannedMeal
  } from '$features/planning/mealPlan.svelte';
  import MoveMealSheet from '$features/planning/MoveMealSheet.svelte';
  import PlanHeader from '$features/planning/PlanHeader.svelte';
  import PlanRecipePicker from '$features/planning/PlanRecipePicker.svelte';
  import PlanShopBar from '$features/planning/PlanShopBar.svelte';
  import PlanWeek from '$features/planning/PlanWeek.svelte';
  import PlanWeekSkeleton from '$features/planning/PlanWeekSkeleton.svelte';
  import { usePlanActions } from '$features/planning/usePlanActions.svelte';
  import { mondayOf, rangeOf } from '$features/planning/weekDates';
  import { WeekDrag } from '$features/planning/weekDrag.svelte';
  import { recipeAnswers } from '$features/nutrition/stores/recipeAnswers.svelte';
  import type { RecipeSummary } from '$features/recipes/types';
  import { session } from '$features/auth/session.svelte';
  import { m } from '$shell/i18n';
  import Olli from '$shell/olli/Olli.svelte';
  import Page from '$shell/Page.svelte';
  import { preferences } from '$shell/preferences.svelte';

  /**
   * The week's plan: seven days, deliberately not a calendar (a month view brings recurrence and a second shopping list); meals move by dragging.
   * Reached from the recipe list, because the navigation bar is deliberately closed at three.
   */
  const householdId = $derived(session.activeHouseholdId);

  /** Which Monday is on screen, as an offset in weeks from this one. */
  let offset = $state(0);

  /** The day an "add" was pressed for, which is also what opens the picker. */
  let adding = $state<string | null>(null);
  let slot = $state<MealSlot>('dinner');

  /** The meal whose move is being answered in the sheet, rather than aimed at. */
  let moving = $state<PlannedMeal | null>(null);

  const monday = $derived(mondayOf(offset));
  const range = $derived(rangeOf(monday, preferences.locale));

  const plannedThisWeek = $derived([
    ...new Set(mealPlan.days.flatMap((day) => day.meals.map((meal) => meal.recipeId)))
  ]);

  const drag = new WeekDrag();
  const actions = usePlanActions({ householdId: () => householdId, monday: () => monday });

  onDestroy(() => {
    drag.stop();
    // The next visit asks again (a 304 when nothing changed), so an edit or a correction made meanwhile shows.
    recipeAnswers.reset();
  });

  $effect(() => {
    if (householdId) {
      void mealPlan.load(householdId, monday);
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

  function moveSheetAnswered(to: { date: string; slot?: MealSlot; position?: number }) {
    const entryId = moving?.entryId;

    moving = null;

    if (entryId) {
      void actions.move(entryId, to);
    }
  }
</script>

{#snippet peeking()}<Olli pose="peeking" />{/snippet}
<svelte:head><title>{m['plan.title']()}</title></svelte:head>

<Page>
  <PlanHeader {range} {offset} onstep={(weeks) => (offset += weeks)} onreset={() => (offset = 0)} />

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
  {:else if mealPlan.loading && mealPlan.days.length === 0}
    <PlanWeekSkeleton />
  {:else}
    <PlanWeek
      days={mealPlan.days}
      {householdId}
      {drag}
      ondrop={actions.drop}
      onadd={addOn}
      onmove={(meal) => (moving = meal)}
      onremove={(meal) => void actions.unplan(meal)}
    />

    {#if mealPlan.meals.length > 0}
      <PlanShopBar
        unshopped={mealPlan.unshopped.length}
        busy={actions.ui.busy}
        onshop={() => void actions.shop()}
      />
    {:else if !mealPlan.loading}
      <EmptyState
        title={m['plan.empty.title']()}
        body={m['plan.empty.description']()}
        art={peeking}
      >
        {#snippet action()}
          <Button variant="primary" href={resolve('/(app)')}>{m['plan.empty.action']()}</Button>
        {/snippet}
      </EmptyState>
    {/if}
  {/if}
</Page>

{#if drag.held}
  <CarriedMeal held={drag.held} at={drag.at} />
{/if}

<MoveMealSheet
  meal={moving}
  days={mealPlan.days}
  from={moving ? (placeOf(mealPlan.days, moving.entryId)?.date ?? monday) : monday}
  onmove={moveSheetAnswered}
  onclose={() => (moving = null)}
/>

{#if householdId}
  <PlanRecipePicker
    open={adding !== null}
    {householdId}
    taken={plannedThisWeek}
    bind:slot
    onpick={(recipe) => void pick(recipe)}
    onclose={() => (adding = null)}
  />
{/if}
