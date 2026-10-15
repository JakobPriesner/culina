<script lang="ts">
  import { resolve } from '$app/paths';
  import { Disclosure } from '$ds';

  import type { MealSlot } from '$features/planning/mealPlan.svelte';
  import { slotLabel } from '$features/planning/slots';
  import { formatDate, m } from '$shell/i18n';
  import type { ListedDay } from './plannedMeals';
  import { amountOf } from './shoppingAmount';

  /**
   * Which planned meals the list is made of, day by day, so a total can be traced to the meal behind it.
   * Days already past are named as such and stay on the list: nothing is dropped for being old, a person ticks or removes it.
   */
  interface Props {
    days: readonly ListedDay[];
  }

  let { days }: Props = $props();

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
          >{m['shopping.meals.title']({ count: meals })}{#if past > 0}
            <span class="past-count">· {m['shopping.meals.pastCount']({ count: past })}</span
            >{/if}</span
        >
      {/snippet}

      {#each days as day (day.date)}
        <section class="day">
          <h3 class="date">
            {dayLabel(day.date)}
            {#if day.isPast}<span class="past">{m['shopping.meals.past']()}</span>{/if}
          </h3>
          {#if day.isPast}<p class="note">{m['shopping.meals.pastNote']()}</p>{/if}

          <ul class="day-meals">
            {#each day.meals as meal (`${meal.recipeId}-${meal.slot ?? ''}`)}
              <li class="meal">
                <p class="meal-name">
                  <a href={resolve('/(app)/recipes/[recipeId]', { recipeId: meal.recipeId })}
                    >{meal.recipeTitle}</a
                  >
                  {#if slotOf(meal.slot)}<span class="slot">{slotOf(meal.slot)}</span>{/if}
                </p>
                <ul class="lines">
                  {#each meal.lines as line (line.itemId)}
                    <li>
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
  .lines {
    gap: var(--space-1);
    padding-inline-start: var(--space-4);
    color: var(--text-muted);
  }
</style>
