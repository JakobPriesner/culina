<script lang="ts">
  import { resolve } from '$app/paths';
  import { Chevron } from '$ds';

  import { cooking } from '$features/cooking/stores/cooking.svelte';
  import { hrefAtYield } from '$features/recipes/surface/yieldInUrl';
  import { m } from '$shell/i18n';
  import { slotLabel } from './slots';
  import type { MealSlot } from './mealPlan.svelte';
  import { upcoming } from './upcoming.svelte';

  /**
   * Today's planned meal, one tap from reading it or cooking it: the hand-off from the week to the hob. Shows the
   * next meal of the day still to cook (see `upcoming.mealNow`), that the day's cooking is done once it
   * is, and nothing when today has no plan or that meal is already on the hob (the cooking pill covers it).
   */
  interface Props {
    householdId: string;
  }

  let { householdId }: Props = $props();

  $effect(() => {
    void upcoming.load(householdId);
  });

  const meal = $derived(upcoming.mealNow);
  /** Shown once nothing is left to cook today: the day's cooking is done, and it says so calmly. */
  const cooked = $derived(meal ? null : (upcoming.cookedToday.at(-1) ?? null));

  const onTheHob = $derived(meal !== null && cooking.session?.recipeId === meal.recipeId);
</script>

{#if meal && !onTheHob}
  <!-- Two ways on from tonight's meal: the recipe to read first, or straight to the hob. -->
  <!-- eslint-disable svelte/no-navigation-without-resolve -- The routes are resolved; only the yield is appended. -->
  <div class="today">
    <a
      class="about"
      href={hrefAtYield(
        resolve('/(app)/recipes/[recipeId]', { recipeId: meal.recipeId }),
        meal.servings
      )}
    >
      <span class="when">{m['plan.today.meal']({ slot: slotLabel[meal.slot as MealSlot]() })}</span>
      <span class="what">{meal.title}</span>
    </a>
    <a
      class="cook"
      href={hrefAtYield(
        resolve('/(app)/recipes/[recipeId]/cook', { recipeId: meal.recipeId }),
        meal.servings
      )}
    >
      {m['plan.today.cook']()}
      <Chevron />
    </a>
  </div>
  <!-- eslint-enable svelte/no-navigation-without-resolve -->
{:else if cooked}
  <div class="today done">
    <a class="about" href={resolve('/(app)/recipes/[recipeId]', { recipeId: cooked.recipeId })}>
      <span class="when">{m['plan.today.cooked']()}</span>
      <span class="what">{cooked.title}</span>
    </a>
    <span class="tick" aria-hidden="true">
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5">
        <path d="m5 12 5 5 9-10" stroke-linecap="round" stroke-linejoin="round" />
      </svg>
    </span>
  </div>
{/if}

<style>
  /* A row like a settings cell that leads on: when and what (the recipe), and the one thing to do with it. */
  .today {
    display: flex;
    align-items: center;
    gap: var(--space-4);
    margin-bottom: var(--space-6);
    padding: var(--space-3) var(--space-4);
    border-radius: var(--radius-lg);
    background: var(--surface-accent-subtle);
    color: var(--text);
    transition: background-color var(--duration-fast) var(--ease-out);
  }

  @media (hover: hover) {
    .today:has(.about:hover) {
      background: var(--surface-highlight);
    }
  }

  .about {
    display: flex;
    flex: 1;
    flex-direction: column;
    min-width: 0;
    color: inherit;
    text-decoration: none;
  }

  .when {
    color: var(--accent);
    font-size: var(--text-sm);
    font-weight: var(--weight-semibold);
  }

  .what {
    overflow: hidden;
    font-family: var(--font-editorial);
    font-size: var(--text-lg);
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .cook {
    display: inline-flex;
    flex: none;
    align-items: center;
    gap: var(--space-1);
    min-height: var(--control-sm);
    padding-inline: var(--space-4);
    border-radius: var(--radius-full);
    background: var(--accent);
    color: var(--accent-contrast);
    font-size: var(--text-sm);
    font-weight: var(--weight-medium);
    text-decoration: none;
    transition: transform var(--duration-fast) var(--ease-out);
  }

  .cook:active {
    transform: scale(var(--press-scale));
  }

  /* Done, not a call to action: no filled pill, the accent kept for the tick. */
  .done {
    background: var(--surface-sunken);
  }

  .done .when {
    color: var(--text-muted);
  }

  .tick {
    display: inline-flex;
    flex: none;
    width: var(--space-6);
    height: var(--space-6);
    color: var(--success);
  }

  @media print {
    .today {
      display: none;
    }
  }
</style>
