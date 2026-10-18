<script lang="ts">
  import { goto } from '$app/navigation';
  import { resolve } from '$app/paths';
  import { Button, Checkbox, Field, Select, Sheet, Skeleton } from '$ds';

  import { shopping } from '$features/shopping/stores/shopping.svelte';

  import { explain } from '$shell/explain';
  import { m } from '$shell/i18n';
  import { preferences } from '$shell/preferences.svelte';
  import { toaster } from '$shell/toaster.svelte';

  import MealFields from './MealFields.svelte';
  import { mealPlan, type MealSlot } from './mealPlan.svelte';
  import { upcoming } from './upcoming.svelte';
  import { dayOf, mondayOf } from './weekDates';

  /**
   * Short path from choosing a recipe to the week: the days from today on (this week and the next,
   * so a Sunday still has a week ahead), the meal, and for how many (the scaling it was opened at).
   * Once the coming meals have been shopped for, a meal planned now would miss the list; it offers to go on too.
   */
  interface Props {
    open: boolean;
    householdId: string;
    recipeId: string;
    title: string;
    servings: number;
    /** What the recipe is written for: planning fewer leaves a rest, which can go on the plan as leftovers. */
    recipeServings?: number;
    onclose: () => void;
  }

  let { open, householdId, recipeId, title, servings, recipeServings, onclose }: Props = $props();

  let date = $state('');
  let slot = $state<MealSlot>('dinner');
  let saving = $state(false);
  let count = $state(1);
  /** The stepper counts whole servings; a page scaled to 2.5 plans 2.5 until somebody says otherwise. */
  let touched = $state(false);
  let alsoShop = $state(false);
  /** The kitchen's usual servings stood in for the recipe's own when the sheet opened. */
  let usual = $state<number | null>(null);
  let leftovers = $state(false);
  let leftoverDate = $state('');

  const days = $derived(upcoming.days);

  $effect(() => {
    if (!open) {
      return;
    }

    date = '';
    slot = 'dinner';
    count = Math.max(1, Math.round(servings));
    touched = false;
    usual = null;
    leftovers = false;
    leftoverDate = '';
    void upcoming.load(householdId);
    void shopping.load(householdId);
  });

  /** Already on the list by itself: planning makes it this meal's shopping (the server links it), so nothing to offer. */
  const listed = $derived(shopping.householdId === householdId && shopping.holdsRecipe(recipeId));

  /** Something coming up is already on the list, so the shop has happened (or is under way) without this meal. */
  const shopped = $derived(
    upcoming.days.some((day) => day.meals.some((meal) => meal.isOnShoppingList))
  );

  $effect(() => {
    if (open) {
      alsoShop = shopped;
    }
  });

  // A page at the recipe's own yield says nothing about this kitchen; its usual number does, once the plan has one.
  $effect(() => {
    const kitchen = upcoming.usualServings;

    if (
      open &&
      !touched &&
      kitchen !== null &&
      recipeServings !== undefined &&
      Math.round(servings) === recipeServings &&
      kitchen !== recipeServings
    ) {
      count = kitchen;
      touched = true;
      usual = kitchen;
    }
  });

  /** For how many this meal is, as it will be saved. */
  const planned = $derived(touched ? count : servings);
  /** What the recipe makes beyond that: a rest to eat another day. */
  const rest = $derived(
    recipeServings !== undefined && planned < recipeServings ? recipeServings - planned : 0
  );
  /** The days after the chosen one, for eating the rest. */
  const later = $derived(days.filter((day) => day.date > date).slice(0, 4));

  $effect(() => {
    if (open && !later.some((day) => day.date === leftoverDate)) {
      leftoverDate = later[0]?.date ?? '';
    }
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

  async function confirm() {
    if (!date || saving) {
      return;
    }

    saving = true;
    const withRest = leftovers && rest > 0 && leftoverDate !== '';
    // With leftovers the batch is cooked (and bought) once, for everything the recipe makes.
    const ok = await mealPlan.plan(householdId, {
      date,
      recipeId,
      servings: withRest ? recipeServings : planned,
      slot
    });
    const original = ok ? plannedEntry() : undefined;
    const restOk =
      withRest && original
        ? await mealPlan.plan(householdId, {
            date: leftoverDate,
            recipeId,
            servings: rest,
            slot: 'lunch',
            leftoverOf: original
          })
        : true;
    const listFailure = ok && alsoShop && !listed ? await shopPlanned(original) : null;
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

    const restDay = withRest && restOk ? longDay.format(dayOf(leftoverDate)) : null;

    toaster.show({
      message: () =>
        listFailure
          ? m['plan.recipe.doneNotListed']({ day })
          : restDay
            ? m['plan.recipe.doneLeftovers']({ day, rest: restDay })
            : alsoShop || listed
              ? m['plan.recipe.doneListed']({ day })
              : m['plan.recipe.done']({ day }),
      tone: listFailure || !restOk ? 'danger' : 'success',
      action: {
        label: () => m['plan.recipe.showWeek'](),
        // eslint-disable-next-line svelte/no-navigation-without-resolve -- The route is resolved; only the week query is appended.
        run: () => void goto(`${resolve('/(app)/plan')}${weekOf}`)
      }
    });
    onclose();
  }

  /** The meal just planned is the last of its kind on that day and in that slot: the server appends it. */
  const plannedEntry = () =>
    mealPlan.days
      .find((day) => day.date === date)
      ?.meals.findLast(
        (meal) => meal.recipeId === recipeId && meal.slot === slot && !meal.leftoverOf
      )?.entryId;

  async function shopPlanned(entryId = plannedEntry()) {
    return entryId ? shopping.addPlannedMeal(householdId, date, entryId) : null;
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
    <MealFields
      {days}
      bind:date
      bind:slot
      bind:servings={count}
      onservings={() => (touched = true)}
      servingsHint={usual !== null && count === usual
        ? m['plan.recipe.usual']({ servings: m['recipes.meta.servings']({ count: usual }) })
        : undefined}
    >
      {#if rest > 0 && later.length > 0}
        <div class="rest">
          <Checkbox bind:checked={leftovers} label={m['plan.recipe.leftovers']({ count: rest })} />
          <p class="hint">{m['plan.recipe.leftoversHint']()}</p>
          {#if leftovers}
            <Field label={m['plan.recipe.leftoversDay']()}>
              {#snippet children({ id })}
                <Select
                  {id}
                  bind:value={leftoverDate}
                  options={later.map((day) => ({
                    value: day.date,
                    label: longDay.format(dayOf(day.date))
                  }))}
                />
              {/snippet}
            </Field>
          {/if}
        </div>
      {/if}
      <!-- Offered every time, ticked once the coming meals are shopped: a meal planned now would miss that shop. -->
      {#if listed}
        <p class="hint">{m['plan.recipe.alreadyListed']()}</p>
      {:else}
        <Checkbox bind:checked={alsoShop} label={m['plan.recipe.alsoShop']()} />
      {/if}
    </MealFields>
  {/if}

  {#snippet footer()}
    <Button onclick={onclose}>{m['plan.recipe.cancel']()}</Button>
    <Button variant="primary" disabled={!date} loading={saving} onclick={confirm}>
      {m['plan.recipe.confirm']()}
    </Button>
  {/snippet}
</Sheet>

<style>
  .loading {
    display: flex;
    flex-direction: column;
    gap: var(--space-6);
  }

  .failure {
    color: var(--text-danger);
  }

  .rest {
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
</style>
