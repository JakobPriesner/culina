<script lang="ts">
  import { SvelteSet } from 'svelte/reactivity';

  import { Button, EmptyState, ErrorState, IconButton } from '$ds';
  import { session } from '$features/auth/session.svelte';
  import DiaryAddSheet, { type FoodChoice } from '$features/diary/DiaryAddSheet.svelte';
  import DiaryAmountSheet, {
    type AmountAnswer,
    type AmountSubject
  } from '$features/diary/DiaryAmountSheet.svelte';
  import DiaryEntryRow from '$features/diary/DiaryEntryRow.svelte';
  import type { TypedFigures } from '$features/diary/DiaryOwnFigures.svelte';
  import DiarySummary from '$features/diary/DiarySummary.svelte';
  import { requestOf, unlogged } from '$features/diary/dayTotals';
  import { addDays, dayLabel, kcalText, mealLabel } from '$features/diary/format';
  import { logAndConfirm, removeAndConfirm } from '$features/diary/logging';
  import { diary } from '$features/diary/stores/diary.svelte';
  import { diarySettings } from '$features/diary/stores/diarySettings.svelte';
  import { currentDate } from '$features/diary/today.svelte';
  import { diaryMeals, type DiaryEntry, type DiaryMeal } from '$features/diary/types';
  import { upcoming } from '$features/planning/upcoming.svelte';
  import RecipePicker from '$features/recipes/RecipePicker.svelte';
  import type { RecipeSummary } from '$features/recipes/types';
  import { explain } from '$shell/explain';
  import { m } from '$shell/i18n';
  import Page from '$shell/Page.svelte';
  import PageHeader from '$shell/PageHeader.svelte';
  import { toaster } from '$shell/toaster.svelte';

  /**
   * A person's food diary, one day as a ruled page (docs/diary-design.md §9.1): what they ate by meal, what it adds up
   * to, and the plan's meals of today as suggestions that count for nothing until confirmed. Private: a tab for
   * everyone, whose page offers the switch (with its consent sentence) until someone turns it on.
   */
  const householdId = $derived(session.activeHouseholdId);
  const today = $derived(currentDate());

  /** A day the person went to; none follows today, so a page left open overnight moves on with it. */
  let picked = $state<string | null>(null);
  const date = $derived(picked ?? today);

  function go(days: number) {
    const to = addDays(date, days);

    picked = to >= today ? null : to;
  }
  const day = $derived(diary.day(date));
  const settings = $derived(diarySettings.settings);

  /** Off, the page offers the switch itself, with the consent sentence the settings show beside theirs. */
  let switching = $state(false);

  async function switchOn(): Promise<void> {
    switching = true;
    const failure = await diarySettings.save({
      enabled: true,
      kcalTarget: settings?.kcalTarget ?? null,
      proteinTarget: settings?.proteinTarget ?? null,
      headline: settings?.headline === 'remaining' ? 'remaining' : 'eaten'
    });
    switching = false;

    if (failure) toaster.show({ message: () => explain(failure), tone: 'danger' });
  }

  // Two weeks at first, so "what you ate before" has something to offer; then each day as it is shown.
  let primed = false;

  $effect(() => {
    if (!diarySettings.enabled) return;

    const shown = date;

    if (!primed) {
      primed = true;
      void diary.load(addDays(today, -13), today);

      return;
    }

    // Today again only when it is not read yet: a new day after midnight.
    if (shown !== today || !diary.day(shown)) void diary.load(shown);
  });

  $effect(() => {
    if (householdId && diarySettings.enabled) void upcoming.load(householdId);
  });

  /** Today's planned meals not yet logged, by meal: a suggestion, never a count. */
  const suggestions = $derived(date !== today ? [] : unlogged(upcoming.today, day?.entries ?? []));

  /** Days with anything logged in the last seven: a count that never resets and never says "lost". */
  const loggedDays = $derived(
    Array.from({ length: 7 }, (_, index) => diary.day(addDays(today, -index))).filter(
      (one) => one && one.entries.length > 0
    ).length
  );

  const entriesOf = (meal: DiaryMeal): DiaryEntry[] =>
    (day?.entries ?? []).filter((entry) => entry.meal === meal);

  const subtotal = (entries: readonly DiaryEntry[]): string | null => {
    const known = entries.filter((entry) => entry.figures.kcal != null);

    if (known.length === 0) return null;

    return kcalText(
      known.reduce((sum, entry) => sum + (entry.figures.kcal ?? 0), 0),
      {
        atLeast: entries.some((entry) => entry.atLeast || entry.figures.kcal == null),
        estimated: entries.some((entry) => entry.estimated)
      }
    );
  };

  // What is being added or changed, and to which meal.
  let adding = $state<DiaryMeal | null>(null);
  let mealShown = $state<DiaryMeal>('lunch');
  let pickingRecipe = $state(false);
  let subject = $state<AmountSubject | null>(null);
  let pending = $state<
    | { readonly kind: 'recipe'; readonly recipe: RecipeSummary }
    | { readonly kind: 'food'; readonly choice: FoodChoice }
    | { readonly kind: 'entry'; readonly entry: DiaryEntry }
    | null
  >(null);
  let busy = $state(false);

  function add(meal: DiaryMeal) {
    mealShown = meal;
    adding = meal;
  }

  function close() {
    adding = null;
    subject = null;
    pending = null;
    pickingRecipe = false;
  }

  async function again(entry: DiaryEntry) {
    busy = true;
    await logAndConfirm(requestOf(entry, date, mealShown));
    busy = false;
    close();
  }

  async function typed(figures: TypedFigures) {
    busy = true;
    await logAndConfirm({ date, meal: mealShown, ...figures });
    busy = false;
    close();
  }

  function food(choice: FoodChoice) {
    adding = null;
    pending = { kind: 'food', choice };
    subject = choice.subject;
  }

  function recipe(picked: RecipeSummary) {
    pickingRecipe = false;
    pending = { kind: 'recipe', recipe: picked };
    subject = { kind: 'recipe', title: picked.title };
  }

  function open(entry: DiaryEntry) {
    mealShown = entry.meal as DiaryMeal;
    pending = { kind: 'entry', entry };
    subject = { kind: 'entry', entry };
  }

  async function answered(answer: AmountAnswer) {
    if (!pending) return;

    busy = true;

    if (pending.kind === 'recipe') {
      await logAndConfirm({
        date,
        meal: answer.meal,
        recipeId: pending.recipe.id,
        portions: answer.amount,
        householdId
      });
    } else if (pending.kind === 'food') {
      await logAndConfirm({
        date,
        meal: answer.meal,
        foodCode: pending.choice.code,
        amount: answer.amount,
        unit: answer.unit,
        householdId: pending.choice.householdId
      });
    } else {
      const entry = pending.entry;
      const amountChanged = answer.amount !== entry.amount && entry.source !== 'typed';
      const written = await diary.change(entry, {
        meal: answer.meal !== entry.meal ? answer.meal : null,
        portions: amountChanged && entry.source === 'recipe' ? answer.amount : null,
        amount: amountChanged && entry.source === 'food' ? answer.amount : null,
        // Own figures go as a whole: a macro cleared becomes unknown.
        ...answer.figures
      });

      if (!written.ok) toaster.show({ message: () => explain(written.error), tone: 'danger' });
    }

    busy = false;
    close();
  }

  async function remove(entry: DiaryEntry) {
    close();
    await removeAndConfirm(entry);
  }

  /** Planned meals being confirmed, so a second tap does not log the same meal twice. */
  const confirming = new SvelteSet<string>();

  /** A planned meal confirmed as eaten: one portion, at the meal it was planned for. */
  async function eaten(meal: (typeof suggestions)[number]) {
    if (confirming.has(meal.entryId)) return;

    confirming.add(meal.entryId);
    await logAndConfirm({
      date,
      meal: meal.slot,
      recipeId: meal.recipeId,
      portions: 1,
      householdId
    });
    confirming.delete(meal.entryId);
  }
</script>

<svelte:head><title>{m['diary.title']()}</title></svelte:head>

<Page width="reading">
  <PageHeader title={m['diary.title']()} eyebrow={dayLabel(date, today)}>
    {#snippet actions()}
      <div class="days">
        <IconButton label={m['diary.dayBefore']()} onclick={() => go(-1)}>
          <svg
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="1.8"
            aria-hidden="true"
            ><path d="m15 6-6 6 6 6" stroke-linecap="round" stroke-linejoin="round" /></svg
          >
        </IconButton>
        <IconButton label={m['diary.dayAfter']()} disabled={date >= today} onclick={() => go(1)}>
          <svg
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="1.8"
            aria-hidden="true"
            ><path d="m9 6 6 6-6 6" stroke-linecap="round" stroke-linejoin="round" /></svg
          >
        </IconButton>
      </div>
    {/snippet}
  </PageHeader>

  {#if settings && !settings.enabled}
    <EmptyState title={m['diary.off.title']()} body={m['me.diary.consent']()}>
      {#snippet action()}
        <Button variant="primary" loading={switching} onclick={() => void switchOn()}
          >{m['diary.off.action']()}</Button
        >
      {/snippet}
    </EmptyState>
  {:else if diary.status === 'failed' && !day}
    <ErrorState title={m['error.unexpected.title']()} body={m['error.unexpected.body']()}>
      {#snippet action()}
        <Button variant="primary" onclick={() => void diary.load(date)}>{m['error.retry']()}</Button
        >
      {/snippet}
    </ErrorState>
  {:else}
    <DiarySummary
      {day}
      kcalTarget={settings?.kcalTarget ?? null}
      proteinTarget={settings?.proteinTarget ?? null}
      headline={settings?.headline === 'remaining' ? 'remaining' : 'eaten'}
    />

    {#each diaryMeals as meal (meal)}
      {@const entries = entriesOf(meal)}
      {@const planned = suggestions.filter((one) => one.slot === meal)}
      <section class="meal" aria-labelledby="meal-{meal}">
        <header class="mealHead">
          <h2 id="meal-{meal}">{mealLabel[meal]()}</h2>
          {#if subtotal(entries)}<span class="subtotal">{subtotal(entries)}</span>{/if}
        </header>
        <ul class="entries">
          {#each entries as entry (entry.entryId)}
            <DiaryEntryRow {entry} onopen={open} />
          {/each}
          {#each planned as one (one.entryId)}
            <li class="suggestion">
              <span class="what">
                <span class="label">{one.title}</span>
                <span class="from">{m['diary.suggestion.fromPlan']()}</span>
              </span>
              <Button
                size="sm"
                loading={confirming.has(one.entryId)}
                onclick={() => void eaten(one)}>{m['diary.suggestion.eaten']()}</Button
              >
            </li>
          {/each}
        </ul>
        <Button size="sm" variant="ghost" onclick={() => add(meal)}
          >+ {m['diary.add.action']()}</Button
        >
      </section>
    {/each}

    <p class="streak">{m['diary.loggedDays']({ count: loggedDays })}</p>
    <p class="source">{m['diary.attribution']()}</p>
  {/if}
</Page>

<DiaryAddSheet
  open={adding !== null}
  meal={mealShown}
  {householdId}
  {busy}
  onagain={(entry) => void again(entry)}
  onrecipe={() => {
    adding = null;
    pickingRecipe = true;
  }}
  onfood={food}
  ontyped={(figures) => void typed(figures)}
  onclose={close}
/>

{#if householdId}
  <RecipePicker
    open={pickingRecipe}
    {householdId}
    title={m['diary.add.title']({ meal: mealLabel[mealShown]() })}
    suggestFor={mealShown === 'snack' ? undefined : mealShown}
    onpick={recipe}
    onclose={close}
  />
{/if}

<DiaryAmountSheet
  {subject}
  meal={mealShown}
  {busy}
  onsave={(answer) => void answered(answer)}
  onremove={(entry) => void remove(entry)}
  onclose={close}
/>

<style>
  .days {
    display: flex;
    gap: var(--space-1);
  }

  .meal {
    margin-block-end: var(--space-6);
  }

  .mealHead {
    display: flex;
    align-items: baseline;
    justify-content: space-between;
    gap: var(--space-4);
    padding-block-end: var(--space-1);
    border-block-end: 1px solid var(--border-strong);
  }

  h2 {
    margin: 0;
    font-size: var(--text-base);
    font-weight: var(--weight-semibold);
  }

  .subtotal,
  .from,
  .streak,
  .source {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .entries {
    margin: 0 0 var(--space-2);
    padding: 0;
    list-style: none;
  }

  /* A suggestion is not an entry: dashed, and it counts for nothing until it is confirmed. */
  .suggestion {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-3);
    margin-block-start: var(--space-2);
    padding: var(--space-2) var(--space-3);
    border: 1px dashed var(--border-strong);
    border-radius: var(--radius-md);
  }

  .what {
    display: flex;
    flex-direction: column;
    min-inline-size: 0;
  }

  .label {
    font-family: var(--font-editorial);
    overflow-wrap: anywhere;
  }

  .streak {
    margin-block: var(--space-8) var(--space-2);
  }

  .source {
    margin: 0;
    font-size: var(--text-xs);
  }
</style>
