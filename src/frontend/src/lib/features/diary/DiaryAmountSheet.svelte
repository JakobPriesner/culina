<script lang="ts">
  import { Button, Field, FilterChip, Select, Sheet, Stepper, TextInput } from '$ds';
  import { unitLabel } from '$features/recipes/quantityLabels';
  import type { Unit } from '$features/recipes/units';
  import { m } from '$shell/i18n';

  import DiaryBack from './DiaryBack.svelte';
  import DiaryOwnFigures, {
    figuresOf,
    noFigures,
    typedFigures,
    type TypedFigures
  } from './DiaryOwnFigures.svelte';
  import { kcalText, mealLabel, portionsText } from './format';
  import { diaryMeals, type DiaryEntry, type DiaryMeal } from './types';

  /**
   * How much was eaten, before it is logged or after: portions of a recipe (in quarters, with the common ones as chips),
   * or an amount of a food in grams or a cited typical unit, with its energy worked out as it is typed. Opened on an
   * entry, it also moves it to another meal or removes it, and corrects the person's own figures.
   */
  export interface FoodUnitChoice {
    readonly unit: string;
    readonly grams: number;
  }

  export type AmountSubject =
    | { readonly kind: 'recipe'; readonly title: string }
    | {
        readonly kind: 'food';
        readonly name: string;
        readonly kcalPer100Grams: number | null;
        readonly units: readonly FoodUnitChoice[];
      }
    | { readonly kind: 'entry'; readonly entry: DiaryEntry };

  export interface AmountAnswer {
    readonly amount: number;
    readonly unit: string;
    readonly meal: DiaryMeal;
    /** For an entry of the person's own figures, the figures as they stand now. */
    readonly figures: TypedFigures | null;
  }

  interface Props {
    subject: AmountSubject | null;
    meal: DiaryMeal;
    busy?: boolean;
    onsave: (answer: AmountAnswer) => void;
    onremove?: (entry: DiaryEntry) => void;
    /** Given when the subject came from the search, which stays open beneath: back to it, as it was left. */
    onback?: () => void;
    onclose: () => void;
  }

  let { subject, meal, busy = false, onsave, onremove, onback, onclose }: Props = $props();

  const entry = $derived(subject?.kind === 'entry' ? subject.entry : null);
  const isRecipe = $derived(
    subject?.kind === 'recipe' || (subject?.kind === 'entry' && subject.entry.source === 'recipe')
  );
  const isTyped = $derived(subject?.kind === 'entry' && subject.entry.source === 'typed');

  let amount = $state(1);
  let unit = $state('portion');
  let chosenMeal = $state<DiaryMeal>('lunch');
  let typedAmount = $state('');
  let own = $state(noFigures());

  // A new subject starts from its own amount: one portion, 100 g, one piece, or what an entry already says.
  $effect(() => {
    if (!subject) return;

    chosenMeal = (entry?.meal as DiaryMeal | undefined) ?? meal;

    if (entry) {
      amount = entry.amount;
      unit = entry.unit;
      own = figuresOf(entry.label, entry.figures);
    } else if (subject.kind === 'recipe') {
      amount = 1;
      unit = 'portion';
    } else if (subject.kind === 'food') {
      const piece = subject.units[0];

      amount = piece ? 1 : 100;
      unit = piece ? piece.unit : 'g';
    }

    typedAmount = String(amount);
  });

  const title = $derived(
    subject?.kind === 'recipe'
      ? subject.title
      : subject?.kind === 'food'
        ? subject.name
        : (entry?.label ?? '')
  );

  /** The energy of what is typed now, for a food whose table value is known; a recipe's is looked up on saving. */
  const preview = $derived.by(() => {
    if (subject?.kind !== 'food' || subject.kcalPer100Grams == null) return null;

    const grams =
      unit === 'g' ? amount : amount * (subject.units.find((one) => one.unit === unit)?.grams ?? 0);

    return kcalText((subject.kcalPer100Grams * grams) / 100, {
      atLeast: false,
      estimated: unit !== 'g'
    });
  });

  const figures = $derived(isTyped ? typedFigures(own) : null);
  const valid = $derived(
    isTyped
      ? figures !== null
      : amount > 0 && (isRecipe ? amount <= 20 : unit !== 'g' || amount <= 5000)
  );

  function typed(value: string) {
    typedAmount = value;
    const parsed = Number(value.replace(',', '.'));

    amount = Number.isFinite(parsed) ? parsed : 0;
  }

  function save() {
    if (valid) onsave({ amount, unit, meal: chosenMeal, figures });
  }
</script>

<Sheet open={subject !== null} {title} closeLabel={m['app.dismiss']()} {onclose}>
  <div class="body">
    {#if onback}<DiaryBack onclick={onback} />{/if}
    {#if isTyped}
      <DiaryOwnFigures bind:fields={own} />
    {:else if isRecipe}
      <Stepper
        id="diary-portions"
        value={amount}
        label={m['diary.amount.portions']()}
        decreaseLabel={m['diary.amount.less']()}
        increaseLabel={m['diary.amount.more']()}
        min={0.25}
        max={20}
        step={0.25}
        onchange={(value) => (amount = value)}
      />
      <div class="chips">
        {#each [0.5, 1, 1.5, 2] as one (one)}
          <FilterChip selected={amount === one} onclick={() => (amount = one)}>
            {portionsText(one)}
          </FilterChip>
        {/each}
      </div>
    {:else}
      <Field label={m['diary.amount.howMuch']()}>
        {#snippet children({ id, describedBy })}
          <!-- With grams the only unit there is no choice to show, so the unit stands beside the number. -->
          <span class="amount">
            <TextInput {id} {describedBy} value={typedAmount} inputmode="decimal" oninput={typed} />
            {#if subject?.kind === 'food' && subject.units.length === 0}
              <span class="unit" aria-hidden="true">g</span>
            {/if}
          </span>
        {/snippet}
      </Field>
      {#if subject?.kind === 'food' && subject.units.length > 0}
        <div class="chips" role="group" aria-label={m['diary.amount.unit']()}>
          <FilterChip selected={unit === 'g'} onclick={() => (unit = 'g')}>g</FilterChip>
          {#each subject.units as one (one.unit)}
            <FilterChip selected={unit === one.unit} onclick={() => (unit = one.unit)}>
              {unitLabel(one.unit as Unit)} · {m['nutrition.about']()}
              {Math.round(one.grams)} g
            </FilterChip>
          {/each}
        </div>
      {:else if entry}
        <p class="note">{unit === 'g' ? 'g' : unitLabel(unit as Unit)}</p>
      {/if}
    {/if}

    {#if preview}<p class="preview">{m['diary.amount.preview']({ kcal: preview })}</p>{/if}

    <Field label={m['diary.amount.meal']()}>
      {#snippet children({ id, describedBy })}
        <Select
          {id}
          {describedBy}
          inline
          value={chosenMeal}
          options={diaryMeals.map((one) => ({ value: one, label: mealLabel[one]() }))}
          onchange={(value) => (chosenMeal = value as DiaryMeal)}
        />
      {/snippet}
    </Field>
  </div>

  {#snippet footer()}
    <div class="actions">
      {#if entry && onremove}
        <Button variant="ghost" onclick={() => onremove(entry)}>{m['diary.remove']()}</Button>
      {/if}
      <Button variant="primary" disabled={!valid} loading={busy} onclick={save}>
        {entry ? m['diary.save']() : m['diary.log']()}
      </Button>
    </div>
  {/snippet}
</Sheet>

<style>
  .body {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
  }

  .chips {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2);
  }

  .note,
  .preview {
    margin: 0;
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .actions {
    display: flex;
    justify-content: flex-end;
    gap: var(--space-2);
  }

  .amount {
    display: flex;
    align-items: center;
    gap: var(--space-3);
  }

  .amount > :global(:first-child) {
    flex: 1;
  }

  .unit {
    color: var(--text-muted);
  }

  /* On a phone the verb fills the bottom of the sheet, where the thumb is, instead of waiting in a corner. */
  @media (width < 48rem) {
    .actions {
      flex: 1;
    }

    .actions > :global(:last-child) {
      flex: 1;
    }
  }
</style>
