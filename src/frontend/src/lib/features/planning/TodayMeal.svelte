<script lang="ts">
  import { resolve } from '$app/paths';
  import { Chevron } from '$ds';

  import { cooking } from '$features/cooking/stores/cooking.svelte';
  import { m } from '$shell/i18n';
  import { slotLabel } from './slots';
  import type { MealSlot } from './mealPlan.svelte';
  import { upcoming } from './upcoming.svelte';

  /**
   * Today's planned meal, one tap from cooking it: the hand-off from the week to the hob. Shows the
   * next meal of the day by the clock (breakfast before 11, lunch before 15, then dinner), and nothing
   * when today has no plan or that meal is already on the hob (the cooking pill covers it).
   */
  interface Props {
    householdId: string;
  }

  let { householdId }: Props = $props();

  $effect(() => {
    void upcoming.load(householdId);
  });

  const slotNow = (): MealSlot => {
    const hour = new Date().getHours();

    return hour < 11 ? 'breakfast' : hour < 15 ? 'lunch' : 'dinner';
  };

  const order: readonly MealSlot[] = ['breakfast', 'lunch', 'dinner'];

  const meal = $derived.by(() => {
    const today = upcoming.today;
    const from = order.indexOf(slotNow());

    // The meal of this part of the day, else the next one still to come, else the last one planned.
    return (
      today.find((planned) => order.indexOf(planned.slot as MealSlot) >= from) ??
      today.at(-1) ??
      null
    );
  });

  const onTheHob = $derived(meal !== null && cooking.session?.recipeId === meal.recipeId);
</script>

{#if meal && !onTheHob}
  <!-- eslint-disable svelte/no-navigation-without-resolve -- The route is resolved; only the yield is appended. -->
  <a
    class="today"
    href="{resolve('/(app)/recipes/[recipeId]/cook', { recipeId: meal.recipeId })}{meal.servings
      ? `?yield=${meal.servings}`
      : ''}"
  >
    <!-- eslint-enable svelte/no-navigation-without-resolve -->
    <span class="when">{m['plan.today.meal']({ slot: slotLabel[meal.slot as MealSlot]() })}</span>
    <span class="what">{meal.title}</span>
    <span class="cook">{m['plan.today.cook']()} <Chevron /></span>
  </a>
{/if}

<style>
  /* A row like a settings cell that leads on: when, what, and the one thing to do with it. */
  .today {
    display: grid;
    grid-template-columns: minmax(0, 1fr) auto;
    grid-template-areas:
      'when cook'
      'what cook';
    align-items: center;
    gap: 0 var(--space-4);
    margin-bottom: var(--space-6);
    padding: var(--space-3) var(--space-4);
    border-radius: var(--radius-lg);
    background: var(--surface-accent-subtle);
    color: var(--text);
    text-decoration: none;
    transition:
      background-color var(--duration-fast) var(--ease-out),
      transform var(--duration-fast) var(--ease-out);
  }

  @media (hover: hover) {
    .today:hover {
      background: var(--surface-highlight);
    }
  }

  .today:active {
    transform: scale(var(--press-scale));
  }

  .when {
    grid-area: when;
    color: var(--accent);
    font-size: var(--text-sm);
    font-weight: var(--weight-semibold);
  }

  .what {
    grid-area: what;
    overflow: hidden;
    font-family: var(--font-editorial);
    font-size: var(--text-lg);
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .cook {
    grid-area: cook;
    display: inline-flex;
    align-items: center;
    gap: var(--space-1);
    min-height: var(--control-sm);
    padding-inline: var(--space-4);
    border-radius: var(--radius-full);
    background: var(--accent);
    color: var(--accent-contrast);
    font-size: var(--text-sm);
    font-weight: var(--weight-medium);
  }

  @media print {
    .today {
      display: none;
    }
  }
</style>
