<script lang="ts">
  import { resolve } from '$app/paths';
  import { Button, Disclosure } from '$ds';

  import type { MealSlot } from '$features/planning/mealPlan.svelte';
  import { slotLabel } from '$features/planning/slots';
  import { upcoming } from '$features/planning/upcoming.svelte';
  import { planDayHref } from '$features/planning/weekDates';
  import { hrefAtYield } from '$features/recipes/surface/yieldInUrl';
  import { formatDate, m } from '$shell/i18n';
  import type { ListedDay, ListedMeal } from './listedMeals';
  import { amountOf } from './shoppingAmount';

  /**
   * Which meals and recipes the list is made of, day by day (recipes added by themselves last), so a total can be
   * traced to what asked for it and any of them taken back off, planned for later or already past. Days already
   * past are named as such and stay: nothing is dropped for being old. Bought lines stay under their meal, struck
   * through, and the meal says how far along it is.
   */
  interface Props {
    days: readonly ListedDay[];
    /** Takes one meal's (or recipe's) unbought lines off the list. */
    onwithdraw?: (meal: ListedMeal) => void;
    /** Beside the week, which shows its own meals: these are the others, and say so. */
    also?: boolean;
  }

  let { days, onwithdraw, also = false }: Props = $props();

  /** At the servings the meal is planned for, while the plan still holds it (today on). */
  const recipeHref = (meal: ListedMeal) =>
    hrefAtYield(
      resolve('/(app)/recipes/[recipeId]', { recipeId: meal.recipeId }),
      meal.planEntryId ? upcoming.mealOf(meal.planEntryId)?.servings : null
    );

  const meals = $derived(days.reduce((total, day) => total + day.meals.length, 0));
  const past = $derived(
    days.filter((day) => day.isPast).reduce((total, day) => total + day.meals.length, 0)
  );

  const dayLabel = (date: string) =>
    formatDate(new Date(`${date}T12:00:00`), { weekday: 'short', day: 'numeric', month: 'short' });

  /** A slot this client does not know is left out rather than guessed at. */
  const slotOf = (slot: string | null | undefined) =>
    (slot && slotLabel[slot as MealSlot]?.()) || '';
</script>

{#if days.length > 0}
  <div class="meals">
    <Disclosure>
      {#snippet summary()}
        <!-- One run of text: as two items of the summary's row, a narrow screen wrapped them into two ragged columns. -->
        <span
          >{also
            ? m['shopping.meals.also']({ count: meals })
            : m['shopping.meals.title']({ count: meals })}{#if past > 0}
            <span class="past-count">· {m['shopping.meals.pastCount']({ count: past })}</span
            >{/if}</span
        >
      {/snippet}

      {#each days as day (day.date ?? 'alone')}
        <section class="day">
          <h3 class="date">
            <!-- A planned day leads back to the plan at that day. -->
            <!-- eslint-disable-next-line svelte/no-navigation-without-resolve -- planDayHref resolves the route; only the day is appended. -->
            {#if day.date}<a href={planDayHref(day.date)}>{dayLabel(day.date)}</a>{:else}{m[
                'shopping.meals.direct'
              ]()}{/if}
            {#if day.isPast}<span class="past">{m['shopping.meals.past']()}</span>{/if}
          </h3>
          {#if day.isPast}<p class="note">{m['shopping.meals.pastNote']()}</p>{/if}

          <ul class="day-meals">
            {#each day.meals as meal (`${meal.recipeId}-${meal.slot ?? ''}`)}
              <li class="meal">
                <div class="meal-head">
                  <p class="meal-name">
                    <!-- eslint-disable-next-line svelte/no-navigation-without-resolve -- The route is resolved; only the yield is appended. -->
                    <a class="hit-target reach" href={recipeHref(meal)}>{meal.recipeTitle}</a>
                    {#if slotOf(meal.slot)}<span class="slot">{slotOf(meal.slot)}</span>{/if}
                  </p>
                  {#if meal.done > 0}
                    <span class="progress">
                      {m['shopping.meals.progress']({ done: meal.done, total: meal.lines.length })}
                    </span>
                  {/if}
                  {#if onwithdraw && (meal.planEntryId || !day.date)}
                    <Button
                      size="sm"
                      label={m['shopping.meals.withdraw.label']({ title: meal.recipeTitle })}
                      onclick={() => onwithdraw(meal)}
                    >
                      {m['shopping.meals.withdraw']()}
                    </Button>
                  {/if}
                </div>
                <ul class="lines">
                  {#each meal.lines as line (line.itemId)}
                    <li class:checked={line.checked}>
                      {line.name}
                      {#if amountOf(line.quantity, line.unit)}<span class="amount"
                          >{amountOf(line.quantity, line.unit)}</span
                        >{/if}
                    </li>
                  {/each}
                </ul>
              </li>
            {/each}
          </ul>
        </section>
      {/each}
    </Disclosure>
  </div>
{/if}

<style>
  .meals {
    margin-block: var(--space-2) var(--space-6);
    font-size: var(--text-sm);
  }
  .past-count {
    /* The block's leading whitespace is dropped, so the gap is drawn. */
    margin-inline-start: 0.3em;
    color: var(--text-muted);
    font-weight: var(--weight-regular);
  }
  .day {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
    padding-block: var(--space-3);
    border-bottom: 1px solid var(--border);
  }
  .date a {
    color: inherit;
    text-underline-offset: 0.2em;
  }
  .date {
    margin: 0;
    font-size: var(--text-sm);
    font-weight: var(--weight-semibold);
  }
  .past {
    margin-inline-start: var(--space-2);
    padding: 0 var(--space-2);
    border: 1px solid var(--border-strong);
    border-radius: var(--radius-full);
    color: var(--text-muted);
    font-size: var(--text-xs);
    font-weight: var(--weight-regular);
  }
  .note {
    margin: 0;
    color: var(--text-muted);
    font-size: var(--text-xs);
  }
  .day-meals,
  .lines {
    display: flex;
    flex-direction: column;
    margin: 0;
    padding: 0;
    list-style: none;
  }
  .day-meals {
    gap: var(--space-3);
  }
  .meal-head {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-2);
  }
  .meal-name {
    margin: 0;
    font-weight: var(--weight-medium);
  }
  .slot,
  .amount {
    margin-inline-start: var(--space-2);
    color: var(--text-muted);
    font-weight: var(--weight-regular);
  }
  .amount {
    font-variant-numeric: tabular-nums;
  }
  .progress {
    margin-inline-start: auto;
    color: var(--text-muted);
    font-size: var(--text-xs);
    font-variant-numeric: tabular-nums;
  }
  .checked {
    color: var(--text-subtle);
    text-decoration: line-through;
  }
  .lines {
    gap: var(--space-1);
    padding-inline-start: var(--space-4);
    color: var(--text-muted);
  }
</style>
