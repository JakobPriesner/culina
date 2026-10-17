<script lang="ts">
  import { goto } from '$app/navigation';
  import { resolve } from '$app/paths';
  import { Button, Checkbox, Sheet, Skeleton } from '$ds';

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
    onclose: () => void;
  }

  let { open, householdId, recipeId, title, servings, onclose }: Props = $props();

  let date = $state('');
  let slot = $state<MealSlot>('dinner');
  let saving = $state(false);
  let count = $state(1);
  /** The stepper counts whole servings; a page scaled to 2.5 plans 2.5 until somebody says otherwise. */
  let touched = $state(false);
  let alsoShop = $state(false);

  const days = $derived(upcoming.days);

  $effect(() => {
    if (!open) {
      return;
    }

    date = '';
    slot = 'dinner';
    count = Math.max(1, Math.round(servings));
    touched = false;
    void upcoming.load(householdId);
  });

  /** Something coming up is already on the list, so the shop has happened (or is under way) without this meal. */
  const shopped = $derived(
    upcoming.days.some((day) => day.meals.some((meal) => meal.isOnShoppingList))
  );

  $effect(() => {
    if (open) {
      alsoShop = shopped;
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
    const ok = await mealPlan.plan(householdId, {
      date,
      recipeId,
      servings: touched ? count : servings,
      slot
    });
    const listFailure = ok && alsoShop ? await shopPlanned() : null;
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
      message: () =>
        listFailure
          ? m['plan.recipe.doneNotListed']({ day })
          : alsoShop
            ? m['plan.recipe.doneListed']({ day })
            : m['plan.recipe.done']({ day }),
      tone: listFailure ? 'danger' : 'success',
      action: {
        label: () => m['plan.recipe.showWeek'](),
        // eslint-disable-next-line svelte/no-navigation-without-resolve -- The route is resolved; only the week query is appended.
        run: () => void goto(`${resolve('/(app)/plan')}${weekOf}`)
      }
    });
    onclose();
  }

  /** The meal just planned is the last of its kind on that day and in that slot: the server appends it. */
  async function shopPlanned() {
    const entryId = mealPlan.days
      .find((day) => day.date === date)
      ?.meals.findLast((meal) => meal.recipeId === recipeId && meal.slot === slot)?.entryId;

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
    >
      {#if shopped}
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
</style>
