<script lang="ts">
  import { SvelteSet } from 'svelte/reactivity';

  import {
    Button,
    EmptyState,
    ErrorState,
    IconButton,
    ListRow,
    ListSection,
    Skeleton,
    WeekStrip,
    type StripDay
  } from '$ds';
  import { session } from '$features/auth/session.svelte';
  import DiaryAddSheet, { type FoodChoice } from '$features/diary/DiaryAddSheet.svelte';
  import DiaryAmountSheet, {
    type AmountAnswer,
    type AmountSubject
  } from '$features/diary/DiaryAmountSheet.svelte';
  import DiaryEntryRow from '$features/diary/DiaryEntryRow.svelte';
  import type { TypedFigures } from '$features/diary/DiaryOwnFigures.svelte';
  import DiarySummary from '$features/diary/DiarySummary.svelte';
  import Figure from '$features/diary/Figure.svelte';
  import { requestOf, unlogged } from '$features/diary/dayTotals';
  import {
    addDays,
    dayDateLine,
    dayTitle,
    mealLabel,
    mondayOfDate,
    portionsText
  } from '$features/diary/format';
  import {
    logAllAndConfirm,
    logAndConfirm,
    mealByClock,
    removeAndConfirm
  } from '$features/diary/logging';
  import { diary } from '$features/diary/stores/diary.svelte';
  import { diarySettings } from '$features/diary/stores/diarySettings.svelte';
  import { skipped } from '$features/diary/stores/skipped.svelte';
  import { currentDate } from '$features/diary/today.svelte';
  import { diaryMeals, type DiaryEntry, type DiaryMeal } from '$features/diary/types';
  import { upcoming } from '$features/planning/upcoming.svelte';
  import RecipePicker from '$features/recipes/RecipePicker.svelte';
  import type { RecipeSummary } from '$features/recipes/types';
  import { explain } from '$shell/explain';
  import { formatDate, m } from '$shell/i18n';
  import Page from '$shell/Page.svelte';
  import PageAction from '$shell/PageAction.svelte';
  import { toaster } from '$shell/toaster.svelte';

  /**
   * What one ate, one day at a time (docs/food-log-redesign.md §4): the day as the large title with its week beside it,
   * one number for the day, then the meals as grouped lists with the plan's meals of today offered as rows to confirm
   * in one tap. Private: a tab for everyone, whose page offers the switch (with its consent sentence) until someone
   * turns it on. From 64rem the summary stays beside the meals.
   */
  const householdId = $derived(session.activeHouseholdId);
  const today = $derived(currentDate());

  /** A day the person went to; none follows today, so a page left open overnight moves on with it. */
  let picked = $state<string | null>(null);
  const date = $derived(picked ?? today);

  function show(to: string) {
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

  // Two weeks at first, so recent foods have something to offer; then each week as the strip shows it.
  let primed = false;
  const monday = $derived(mondayOfDate(date));

  $effect(() => {
    if (!diarySettings.enabled) return;

    const from = monday;

    if (!primed) {
      primed = true;
      void diary.load(addDays(today, -13), today);

      return;
    }

    // A week not read yet (the strip moved), or today again after midnight.
    const to = addDays(from, 6) > today ? today : addDays(from, 6);
    if (!diary.day(from) || !diary.day(to)) void diary.load(from, to);
  });

  $effect(() => {
    if (householdId && diarySettings.enabled) void upcoming.load(householdId);
  });

  /** The week around the day shown, as the strip draws it: a dot where something is logged, nothing to come. */
  const week = $derived<StripDay[]>(
    Array.from({ length: 7 }, (_, index) => {
      const id = addDays(monday, index);
      const at = new Date(`${id}T12:00:00`);

      return {
        id,
        weekday: formatDate(at, { weekday: 'short' }).replace('.', ''),
        number: String(at.getDate()),
        label: formatDate(at, { weekday: 'long', day: 'numeric', month: 'long' }),
        marked: (diary.day(id)?.entries.length ?? 0) > 0,
        disabled: id > today
      };
    })
  );

  /** Today's planned meals not yet logged nor skipped, by meal: offered, never counted. */
  const suggestions = $derived(
    date !== today
      ? []
      : unlogged(upcoming.today, day?.entries ?? []).filter(
          (meal) => !skipped.has(date, meal.entryId)
        )
  );

  const entriesOf = (meal: DiaryMeal): DiaryEntry[] =>
    (day?.entries ?? []).filter((entry) => entry.meal === meal);

  /** A meal's energy, with how far to trust it: unknown figures make it a lower bound. */
  const subtotalOf = (entries: readonly DiaryEntry[]) => {
    const known = entries.filter((entry) => entry.figures.kcal != null);

    return known.length === 0
      ? null
      : {
          kcal: known.reduce((sum, entry) => sum + (entry.figures.kcal ?? 0), 0),
          atLeast: entries.some((entry) => entry.atLeast || entry.figures.kcal == null),
          estimated: entries.some((entry) => entry.estimated)
        };
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

  /** The search stays open under the amount, so Back finds it as it was left; closing leaves both. */
  function food(choice: FoodChoice) {
    pending = { kind: 'food', choice };
    subject = choice.subject;
  }

  function recipe(picked: RecipeSummary) {
    pickingRecipe = false;
    pending = { kind: 'recipe', recipe: picked };
    subject = { kind: 'recipe', title: picked.title };
  }

  function back() {
    subject = null;
    pending = null;
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

  /** A planned meal confirmed as eaten, in one tap: one portion, at the meal it was planned for. */
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

  /** Yesterday's entries at a meal still empty today: offered as one tap, the quickest way to log a usual breakfast. */
  const yesterdayAt = (meal: DiaryMeal, todays: readonly DiaryEntry[]): DiaryEntry[] =>
    date !== today || todays.length > 0
      ? []
      : (diary.day(addDays(today, -1))?.entries ?? []).filter((entry) => entry.meal === meal);

  const labelsOf = (entries: readonly DiaryEntry[]) =>
    entries.map((entry) => entry.label).join(', ');

  async function repeat(meal: DiaryMeal, entries: readonly DiaryEntry[]) {
    busy = true;
    await logAllAndConfirm(entries.map((entry) => requestOf(entry, date, meal)));
    busy = false;
  }

  /** "Übersprungen": the plan stops offering it today; a neutral answer, undone as easily. */
  function skip(meal: (typeof suggestions)[number]) {
    skipped.skip(date, meal.entryId, today);
    toaster.show({
      message: () => m['diary.planned.skipped']({ title: meal.title }),
      action: {
        label: () => m['diary.undo'](),
        run: () => skipped.unskip(date, meal.entryId, today)
      }
    });
  }
</script>

<svelte:head><title>{m['diary.title']()}</title></svelte:head>

{#snippet plus()}
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"
    ><path d="M12 5v14M5 12h14" stroke-linecap="round" /></svg
  >
{/snippet}

{#snippet circle()}
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" aria-hidden="true"
    ><circle cx="12" cy="12" r="8.5" /></svg
  >
{/snippet}

<Page>
  <div class="log">
    <div class="side">
      <header class="day">
        <div>
          <h1>{dayTitle(date, today)}</h1>
          <p class="date">{dayDateLine(date, today)}</p>
        </div>
        <!-- The page's one accent: in from the top, for the meal the clock suggests; each meal's own "+" below stays quiet. -->
        <!-- Not beside a failed day's "Erneut versuchen", which is then the one to press. -->
        {#if settings?.enabled && !(diary.status === 'failed' && !day)}
          <PageAction text={m['diary.log']()} onclick={() => add(mealByClock())} />
        {/if}
      </header>

      {#if !settings || settings.enabled}
        <div class="aside">
          <div class="calendar">
            <WeekStrip
              days={week}
              selected={date}
              {today}
              label={m['diary.week.label']({
                date: formatDate(new Date(`${monday}T12:00:00`), { day: 'numeric', month: 'long' })
              })}
              onselect={show}
              previousLabel={m['diary.week.previous']()}
              nextLabel={m['diary.week.next']()}
              onprevious={() => show(addDays(date, -7))}
              onnext={() => show(addDays(date, 7))}
              nextDisabled={monday >= mondayOfDate(today)}
            />
          </div>

          {#if settings?.enabled && !day && diary.status !== 'failed'}
            <div class="figures loading-summary" aria-hidden="true">
              <Skeleton width="10rem" height="2.5rem" shape="block" />
              <Skeleton width="100%" height="0.375rem" shape="block" />
              <Skeleton width="14rem" height="1rem" />
            </div>
          {:else if settings?.enabled && day}
            <div class="figures">
              <DiarySummary
                {day}
                kcalTarget={settings?.kcalTarget ?? null}
                proteinTarget={settings?.proteinTarget ?? null}
                headline={settings?.headline === 'remaining' ? 'remaining' : 'eaten'}
              />
            </div>
          {/if}
        </div>
      {/if}
    </div>

    <div class="meals" class:alone={settings && !settings.enabled}>
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
            <Button variant="primary" onclick={() => void diary.load(date)}
              >{m['error.retry']()}</Button
            >
          {/snippet}
        </ErrorState>
      {:else}
        {#if !day}
          <!-- The day's shape while it arrives: four meal cards, a row or two each, nothing counted yet. -->
          <div class="loading" aria-busy="true" aria-label={m['diary.title']()}>
            {#each [2, 1, 1, 0] as rows, index (index)}
              <Skeleton width="100%" height={`${3.5 * (rows + 1)}rem`} shape="block" />
            {/each}
          </div>
        {:else}
          {#each diaryMeals as meal (meal)}
            {@const entries = entriesOf(meal)}
            {@const planned = suggestions.filter((one) => one.slot === meal)}
            {@const subtotal = subtotalOf(entries)}
            {@const before = yesterdayAt(meal, entries)}
            {#snippet sum()}
              {#if subtotal}<Figure {...subtotal} />{/if}
            {/snippet}
            {#snippet addTo()}
              <IconButton
                label={m['diary.add.title']({ meal: mealLabel[meal]() })}
                size="sm"
                onclick={() => add(meal)}>{@render plus()}</IconButton
              >
            {/snippet}
            {#if entries.length + planned.length + before.length === 0}
              <ListSection title={mealLabel[meal]()} trailing={sum} action={addTo} />
            {:else}
              <ListSection title={mealLabel[meal]()} trailing={sum} action={addTo}>
                {#each entries as entry (entry.entryId)}
                  <DiaryEntryRow {entry} onopen={open} />
                {/each}
                {#each planned as one (one.entryId)}
                  <ListRow>
                    {#snippet leading()}
                      <IconButton
                        label={m['diary.planned.log']({ title: one.title })}
                        disabled={confirming.has(one.entryId)}
                        onclick={() => void eaten(one)}>{@render circle()}</IconButton
                      >
                    {/snippet}
                    <span class="planned-title">{one.title}</span>
                    <span class="planned-detail"
                      >{m['diary.planned']()} · {m['diary.portions']({
                        count: 1,
                        amount: portionsText(1)
                      })}</span
                    >
                    {#snippet trailing()}
                      <Button
                        size="sm"
                        variant="ghost"
                        label={m['diary.planned.skipLabel']({ title: one.title })}
                        onclick={() => skip(one)}>{m['diary.planned.skip']()}</Button
                      >
                    {/snippet}
                  </ListRow>
                {/each}
                {#if before.length > 0}
                  <ListRow
                    tone="action"
                    label={m['diary.repeat.label']({ items: labelsOf(before) })}
                    onclick={() => void repeat(meal, before)}
                  >
                    <span>{m['diary.repeat']()}</span>
                    <span class="repeat-detail">{labelsOf(before)}</span>
                  </ListRow>
                {/if}
              </ListSection>
            {/if}
          {/each}
        {/if}

        <p class="source">{m['diary.attribution']()}</p>
      {/if}
    </div>
  </div>
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
  onpickrecipe={recipe}
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
  onback={adding === null ? undefined : back}
  onclose={close}
/>

<style>
  .log {
    display: grid;
    gap: var(--space-6);
    inline-size: 100%;
    max-inline-size: 45rem;
    margin-inline: auto;
  }

  .side,
  .aside {
    display: flex;
    flex-direction: column;
    gap: var(--space-6);
  }

  .day {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-4);
  }

  .day h1 {
    margin: 0;
    font-family: var(--font-editorial);
    font-size: var(--text-3xl);
    font-weight: var(--weight-regular);
    letter-spacing: -0.02em;
  }

  .date {
    margin: var(--space-1) 0 0;
    color: var(--text-muted);
  }

  .meals {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
    min-inline-size: 0;
  }

  /* Below the week and numbers, which the page shows whenever the meals are not alone. */
  .meals:not(.alone) {
    margin-block-start: var(--space-2);
  }

  .loading-summary {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
  }

  .loading {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
  }

  .planned-title {
    font-weight: var(--weight-medium);
  }

  .planned-detail,
  .repeat-detail,
  .source {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .calendar,
  .figures {
    min-inline-size: 0;
  }

  /* Portrait tablets keep one reading column. The week and its totals share a compact overview instead of
     stretching seven dates and three figures across the whole page. Phones keep their full-width week. */
  @media (48rem <= width < 64rem) {
    .aside {
      display: grid;
      grid-template-columns: minmax(0, 1.1fr) minmax(0, 1fr);
      align-items: center;
      padding: var(--space-4);
      border-radius: var(--radius-lg);
      background: var(--surface-raised);
    }

    .calendar:only-child {
      grid-column: 1 / -1;
    }

    .figures {
      padding-inline-start: var(--space-6);
      border-inline-start: 1px solid var(--border);
    }
  }

  .source {
    margin: var(--space-2) 0 0;
    font-size: var(--text-xs);
  }

  /*
   * From 64rem the day's title, its week and numbers stand still in their own column, the week and numbers on one
   * raised card, the meals' own surface (docs/food-log-redesign.md §4.2); only the meals beside them scroll.
   */
  @media (min-width: 64rem) {
    .log {
      grid-template-columns: minmax(20rem, 24rem) minmax(0, 52rem);
      align-items: start;
      gap: var(--space-8) var(--space-12);
      max-inline-size: none;
      margin-inline: 0;
    }

    .side {
      position: sticky;
      inset-block-start: var(--pinned-top);
    }

    .day {
      align-items: center;
    }

    .aside {
      padding: var(--space-6);
      border-radius: var(--radius-lg);
      background: var(--surface-raised);
    }

    .meals:not(.alone) {
      margin-block-start: 0;
    }

    .meals.alone {
      grid-column: 1 / -1;
    }
  }
</style>
