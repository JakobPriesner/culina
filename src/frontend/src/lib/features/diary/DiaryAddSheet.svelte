<script lang="ts">
  import { Button, SearchField, SegmentedControl, Sheet } from '$ds';
  import { searchFoods } from '$features/nutrition/stores/foods';
  import { householdFoods } from '$features/nutrition/stores/householdFoods.svelte';
  import { foldText } from '$features/recipes/foldText';
  import { m } from '$shell/i18n';

  import type { AmountSubject } from './DiaryAmountSheet.svelte';
  import DiaryOwnFigures, {
    noFigures,
    typedFigures,
    type TypedFigures
  } from './DiaryOwnFigures.svelte';
  import { amountText, kcalText, mealLabel } from './format';
  import { diary } from './stores/diary.svelte';
  import type { DiaryEntry, DiaryMeal } from './types';

  /**
   * What was eaten at one meal, the quick way first (docs/diary-design.md §6, §9.4): what this person ate before
   * (one tap), a recipe of the household, a food from the table or the household's own (then an amount), or their
   * own figures for a café lunch. Figures are always the server's, except the person's own.
   */
  export interface FoodChoice {
    readonly code: string;
    readonly subject: Extract<AmountSubject, { kind: 'food' }>;
    /** For a household's own food, whose. */
    readonly householdId: string | null;
  }

  interface Props {
    open: boolean;
    meal: DiaryMeal;
    householdId: string | null;
    busy?: boolean;
    onagain: (entry: DiaryEntry) => void;
    onrecipe: () => void;
    onfood: (choice: FoodChoice) => void;
    ontyped: (figures: TypedFigures) => void;
    onclose: () => void;
  }

  let {
    open,
    meal,
    householdId,
    busy = false,
    onagain,
    onrecipe,
    onfood,
    ontyped,
    onclose
  }: Props = $props();

  type Way = 'recent' | 'foods' | 'own';

  const recent = $derived(diary.recent(8, meal));
  let way = $state<Way>('recent');
  let query = $state('');
  let found = $state<FoodChoice[]>([]);
  let searching = 0;

  let own = $state(noFigures());

  $effect(() => {
    if (open) {
      way = recent.length > 0 ? 'recent' : 'foods';
      if (householdId) void householdFoods.load(householdId);
    }
  });

  /** The household's own foods first (what is in the cupboard), then the table's. */
  async function search(text: string) {
    query = text;
    const asked = ++searching;
    const wanted = foldText(text.trim());

    if (wanted.length < 2) {
      found = [];
      return;
    }

    const own: FoodChoice[] = householdFoods.foods
      .filter((food) => foldText(food.name).includes(wanted))
      .map((food) => ({
        code: food.code,
        householdId,
        subject: { kind: 'food', name: food.name, kcalPer100Grams: food.energyKcal, units: [] }
      }));
    const table = await searchFoods(text.trim(), 20);

    if (asked !== searching) return;

    found = [
      ...own,
      ...(table.ok
        ? table.foods.map((food) => ({
            code: food.code,
            householdId: null,
            subject: {
              kind: 'food' as const,
              name: food.nameDe,
              kcalPer100Grams: food.energyKcal,
              units: food.units ?? []
            }
          }))
        : [])
    ];
  }

  const typed = $derived(typedFigures(own));

  function logTyped() {
    if (!typed) return;

    ontyped(typed);
    own = noFigures();
  }
</script>

<Sheet
  {open}
  title={m['diary.add.title']({ meal: mealLabel[meal]() })}
  closeLabel={m['app.dismiss']()}
  anchor="top"
  {onclose}
>
  <div class="body">
    <SegmentedControl
      label={m['diary.add.how']()}
      selected={way}
      segments={[
        { id: 'recent', label: m['diary.add.recent']() },
        { id: 'foods', label: m['diary.add.foods']() },
        { id: 'own', label: m['diary.add.own']() }
      ]}
      onselect={(id) => (way = id as Way)}
    />

    <Button variant="secondary" full onclick={onrecipe}>{m['diary.add.recipe']()}</Button>

    {#if way === 'recent'}
      {#if recent.length === 0}
        <p class="note">{m['diary.add.recentEmpty']()}</p>
      {:else}
        <ul class="list">
          {#each recent as entry (entry.entryId)}
            <li>
              <button class="pick" type="button" disabled={busy} onclick={() => onagain(entry)}>
                <span class="name">{entry.label}</span>
                <span class="detail">
                  {[
                    amountText(entry),
                    entry.figures.kcal == null ? null : kcalText(entry.figures.kcal, entry)
                  ]
                    .filter(Boolean)
                    .join(' · ')}
                </span>
              </button>
            </li>
          {/each}
        </ul>
      {/if}
    {:else if way === 'foods'}
      <SearchField
        id="diary-food-search"
        value={query}
        placeholder={m['diary.add.searchPlaceholder']()}
        label={m['diary.add.foods']()}
        clearLabel={m['app.dismiss']()}
        oninput={(text) => void search(text)}
        onclear={() => void search('')}
      />
      <ul class="list">
        {#each found as food (food.code)}
          <li>
            <button class="pick" type="button" onclick={() => onfood(food)}>
              <span class="name">{food.subject.name}</span>
              <span class="detail">
                {#if food.subject.kcalPer100Grams != null}
                  {m['diary.add.per100']({
                    kcal: kcalText(food.subject.kcalPer100Grams, {
                      atLeast: false,
                      estimated: false
                    })
                  })}
                {/if}
                {#if food.householdId}· {m['diary.add.ownFood']()}{/if}
              </span>
            </button>
          </li>
        {/each}
      </ul>
    {:else}
      <p class="note">{m['diary.add.ownHint']()}</p>
      <DiaryOwnFigures bind:fields={own} />
      <Button variant="primary" disabled={!typed} loading={busy} onclick={logTyped}>
        {m['diary.log']()}
      </Button>
    {/if}
  </div>
</Sheet>

<style>
  .body {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
  }

  .list {
    margin: 0;
    padding: 0;
    list-style: none;
  }

  .pick {
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
    inline-size: 100%;
    min-block-size: var(--control-sm);
    padding: var(--space-2) 0;
    border: 0;
    border-block-end: 1px solid var(--border);
    background: transparent;
    color: var(--text);
    font: inherit;
    text-align: start;
    cursor: pointer;
  }

  .pick:hover .name {
    text-decoration: underline;
    text-decoration-color: var(--border-strong);
  }

  .name {
    overflow-wrap: anywhere;
  }

  .detail,
  .note {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .note {
    margin: 0;
  }
</style>
