<script lang="ts">
  import { resolve } from '$app/paths';
  import { Button, EmptyState, ErrorState } from '$ds';

  import PlannedCard from '$features/planning/PlannedCard.svelte';
  import { asDate, mealPlan, type MealSlot } from '$features/planning/mealPlan.svelte';
  import { cookbooks } from '$features/cookbooks/stores/cookbooks.svelte';
  import RecipePicker from '$features/recipes/RecipePicker.svelte';
  import type { RecipeSummary } from '$features/recipes/types';
  import { session } from '$features/auth/session.svelte';
  import { shopping } from '$features/shopping/stores/shopping.svelte';
  import { toaster } from '$shell/toaster.svelte';
  import { m } from '$shell/i18n';
  import Page from '$shell/Page.svelte';
  import { preferences } from '$shell/preferences.svelte';

  /**
   * What this household means to cook this week.
   *
   * Seven days, and deliberately not a calendar. A week is the unit people
   * actually plan in — you shop at the weekend for the week that follows — and
   * a month view is where recurrence, drag-and-drop and a second shopping list
   * come from.
   *
   * It is reached from the recipe list rather than from the navigation bar,
   * which is closed at three on purpose: a plan is a weekly thing, and anything
   * rarer than daily belongs behind one of the three rather than beside it.
   */
  const householdId = $derived(session.activeHouseholdId);

  /** A new Date, never an adjusted one. */
  const addDays = (from: Date, days: number) =>
    new Date(from.getFullYear(), from.getMonth(), from.getDate() + days);

  /** Midday, so a time zone west of UTC cannot move a date to the day before. */
  const dayOf = (date: string) => new Date(`${date}T12:00:00`);

  /** Which Monday is on screen, as an offset in weeks from this one. */
  let offset = $state(0);

  /** The day an "add" was pressed for, which is also what opens the picker. */
  let adding = $state<string | null>(null);
  let slot = $state<MealSlot>('dinner');

  /** Which shelf the picker is searching, or null for everything. */
  let narrowedTo = $state<string | null>(null);
  let busy = $state(false);

  const monday = $derived.by(() => {
    const now = new Date();

    // Built by arithmetic rather than by mutating a Date: a date that is
    // adjusted in place is a date somebody else is holding a reference to.
    return asDate(addDays(now, -((now.getDay() + 6) % 7) + offset * 7));
  });

  /**
   * The week, worded.
   *
   * `formatRange` rather than two formatted dates with a dash between them: it
   * knows not to say the month twice, and it knows which side of the number the
   * month goes on. "September 7 – 13", not "September 7 – September 13".
   */
  const heading = $derived.by(() => {
    const start = dayOf(monday);

    return new Intl.DateTimeFormat(preferences.locale, {
      day: 'numeric',
      month: 'long'
    }).formatRange(start, addDays(start, 6));
  });

  /**
   * The weekday and the number, formatted apart and stacked.
   *
   * Asking Intl for both at once hands back a different word order per locale —
   * and for bare "en" an order that reads oddly in a column heading. Two lines
   * fit a narrow column better anyway, and the month is already in the heading
   * above, so a bare number is not ambiguous.
   */
  const weekdays = $derived(new Intl.DateTimeFormat(preferences.locale, { weekday: 'long' }));

  const today = asDate(new Date());

  $effect(() => {
    if (householdId) {
      void mealPlan.load(householdId, monday);
    }
  });

  // The shelves, so the picker can offer to narrow to one. Only worth asking
  // for once the picker can be opened, which is whenever this page is.
  $effect(() => {
    if (householdId) {
      void cookbooks.list(householdId);
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

  /**
   * Puts the week's shopping on the list, one recipe at a time.
   *
   * The same call the recipe page makes, repeated. A second code path that
   * merged a whole week at once would be a second place for merging to be
   * subtly different, and merging is the entire value of the list.
   */
  async function shop() {
    if (!householdId) {
      return;
    }

    busy = true;

    for (const meal of mealPlan.meals) {
      await shopping.addRecipe(householdId, meal.recipeId, meal.servings ?? meal.recipeServings);
    }

    busy = false;

    toaster.show({
      message: m['plan.addedToList']({ count: mealPlan.meals.length }),
      tone: 'success',
      action: {
        label: m['plan.openList'](),
        run: () => void goToList()
      }
    });
  }

  const goToList = async () => {
    const { goto } = await import('$app/navigation');

    await goto(resolve('/(app)/shopping'));
  };
</script>

<svelte:head><title>{m['plan.title']()}</title></svelte:head>

<Page>
  <header class="head">
    <div class="heading">
      <h1 class="title">{m['plan.title']()}</h1>
      <p class="range">{heading}</p>
    </div>

    <div class="weeks">
      <Button size="sm" onclick={() => (offset -= 1)}>← {m['plan.previous']()}</Button>
      {#if offset !== 0}
        <Button size="sm" onclick={() => (offset = 0)}>{m['plan.thisWeek']()}</Button>
      {/if}
      <Button size="sm" onclick={() => (offset += 1)}>{m['plan.next']()} →</Button>
    </div>
  </header>

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
  {:else}
    <ol class="week">
      {#each mealPlan.days as day (day.date)}
        <li class="day" class:today={day.date === today}>
          <h2 class="name">
            {weekdays.format(dayOf(day.date))}
            <span class="number">{Number(day.date.slice(-2))}</span>
          </h2>

          {#if day.meals.length > 0}
            <ul class="meals">
              {#each day.meals as meal (meal.entryId)}
                <li>
                  <PlannedCard
                    {meal}
                    onremove={() => householdId && void mealPlan.unplan(householdId, meal.entryId)}
                  />
                </li>
              {/each}
            </ul>
          {/if}

          <!-- Every day offers it, planned or not. An "add" that only appears
               on an empty day is an add you cannot use twice. -->
          <Button
            size="sm"
            variant="ghost"
            onclick={() => {
              adding = day.date;
              slot = 'dinner';
            }}
          >
            + {m['plan.add']()}
          </Button>
        </li>
      {/each}
    </ol>

    {#if mealPlan.meals.length > 0}
      <div class="shop">
        <Button variant="primary" loading={busy} onclick={() => void shop()}>
          {m['plan.toShoppingList']()}
        </Button>
      </div>
    {:else if !mealPlan.loading}
      <EmptyState title={m['plan.empty.title']()} body={m['plan.empty.description']()}>
        {#snippet action()}
          <Button variant="primary" href={resolve('/(app)')}>{m['plan.empty.action']()}</Button>
        {/snippet}
      </EmptyState>
    {/if}
  {/if}
</Page>

{#if householdId}
  <RecipePicker
    open={adding !== null}
    {householdId}
    title={m['plan.pick.title']()}
    cookbookId={narrowedTo ?? undefined}
    onpick={(recipe) => void pick(recipe)}
    onclose={() => (adding = null)}
  >
    <!-- Which meal, and only here. A slot picker on the week view would put
         three empty rows on every day for the household that only plans
         dinner, which is most of them. -->
    {#snippet controls()}
      <!-- Narrowing to a shelf, and only when the household has one. "What are
           we cooking Thursday" is usually asked of a subset somebody has
           already chosen, and this is that subset. -->
      {#if cookbooks.items.length > 0}
        <fieldset class="slots">
          <legend>{m['cookbooks.title']()}</legend>
          <label>
            <input type="radio" name="cookbook" value={null} bind:group={narrowedTo} />
            {m['cookbooks.picker.all']()}
          </label>
          {#each cookbooks.items as cookbook (cookbook.id)}
            <label>
              <input type="radio" name="cookbook" value={cookbook.id} bind:group={narrowedTo} />
              {cookbook.name}
            </label>
          {/each}
        </fieldset>
      {/if}

      <fieldset class="slots">
        <legend>{m['plan.pick.slot']()}</legend>
        {#each ['breakfast', 'lunch', 'dinner'] as const as which (which)}
          <label>
            <input type="radio" name="slot" value={which} bind:group={slot} />
            {m[`plan.slot.${which}`]()}
          </label>
        {/each}
      </fieldset>
    {/snippet}
  </RecipePicker>
{/if}

<style>
  .head {
    display: flex;
    flex-wrap: wrap;
    align-items: baseline;
    justify-content: space-between;
    gap: var(--space-4);
    margin-bottom: var(--space-6);
  }

  .title {
    font-family: var(--font-editorial);
    font-size: var(--text-2xl);
    font-weight: var(--weight-regular);
  }

  .range {
    color: var(--text-muted);
  }

  .weeks {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2);
  }

  /* Day cards stay chronological; seven columns only when each day is usable. */
  .week {
    display: grid;
    grid-template-columns: minmax(0, 1fr);
    gap: var(--space-3);
    margin: 0;
    padding: 0;
    list-style: none;
  }

  .day {
    display: flex;
    flex-direction: column;
    align-items: flex-start;
    gap: var(--space-2);
    min-width: 0;
    padding: var(--space-3);
    border-radius: var(--radius-md);
    background: var(--surface-sunken);
  }

  /* Lit rather than boxed: today keeps its place in the row and the eye finds
     it without the layout moving. */
  .today {
    background: var(--surface-accent-subtle);
  }

  .name {
    display: flex;
    align-items: baseline;
    flex-wrap: wrap;
    gap: var(--space-2);
    color: var(--text-subtle);
    font-size: var(--text-xs);
    letter-spacing: 0.04em;
    text-transform: uppercase;
  }

  .number {
    color: var(--text-muted);
    font-size: var(--text-sm);
    font-variant-numeric: tabular-nums;
    letter-spacing: normal;
  }

  .meals {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
    width: 100%;
    margin: 0;
    padding: 0;
    list-style: none;
  }

  .shop {
    margin-top: var(--space-6);
  }

  .slots {
    display: flex;
    flex-wrap: wrap;
    min-width: 0;
    gap: var(--space-4);
    padding: 0;
    border: 0;
  }

  .slots legend {
    color: var(--text-subtle);
    font-size: var(--text-xs);
    letter-spacing: 0.08em;
    text-transform: uppercase;
  }

  .slots label {
    min-height: var(--control-sm);
    display: flex;
    align-items: center;
    gap: var(--space-2);
  }

  @media (min-width: 40rem) {
    .week {
      grid-template-columns: repeat(2, minmax(0, 1fr));
    }
  }

  @media (min-width: 64rem) {
    .week {
      grid-template-columns: repeat(3, minmax(0, 1fr));
    }
  }

  @media (min-width: 80rem) {
    .week {
      grid-template-columns: repeat(7, minmax(0, 1fr));
    }

    .name {
      flex-direction: column;
    }
  }
</style>
