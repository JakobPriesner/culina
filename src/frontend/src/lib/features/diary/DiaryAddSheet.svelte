<script lang="ts">
  import { Button, Chevron, ListRow, ListSection, SearchField, Sheet } from '$ds';
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

  /** The sheet's one path (Apple's "single path through subviews"): pick something, or step into typing figures. */
  let view = $state<'pick' | 'own'>('pick');

  const recent = $derived(diary.recent(8, meal));
  let query = $state('');
  let found = $state<FoodChoice[]>([]);
  /** The query the results in `found` answer, so "nothing found" is said only once the search has come back. */
  let answered = $state('');
  let searching = 0;

  let own = $state(noFigures());

  /** Typing turns the sheet from what was eaten before into what the table holds. */
  const looking = $derived(foldText(query.trim()).length >= 2);

  $effect(() => {
    if (open) {
      view = 'pick';
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
      answered = '';
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
    answered = text;
  }

  const typed = $derived(typedFigures(own));

  function logTyped() {
    if (!typed) return;

    ontyped(typed);
    own = noFigures();
  }

  function again(entry: DiaryEntry) {
    if (!busy) onagain(entry);
  }
</script>

{#snippet logOwn()}
  <Button variant="primary" full disabled={!typed} loading={busy} onclick={logTyped}>
    {m['diary.log']()}
  </Button>
{/snippet}

{#snippet plus()}
  <svg
    class="plus"
    viewBox="0 0 24 24"
    fill="none"
    stroke="currentColor"
    stroke-width="2"
    stroke-linecap="round"
    aria-hidden="true"
  >
    <path d="M12 5v14M5 12h14" />
  </svg>
{/snippet}

<Sheet
  {open}
  title={m['diary.add.title']({ meal: mealLabel[meal]() })}
  closeLabel={m['app.dismiss']()}
  anchor="top"
  size="large"
  grouped
  footer={view === 'own' ? logOwn : undefined}
  {onclose}
>
  {#if view === 'own'}
    <div class="own">
      <button class="back" type="button" onclick={() => (view = 'pick')}>
        <Chevron direction="back" />
        {m['diary.add.back']()}
      </button>
      <h3 class="own-title">{m['diary.add.own']()}</h3>
      <p class="note">{m['diary.add.ownHint']()}</p>
      <DiaryOwnFigures bind:fields={own} />
    </div>
  {:else}
    <!-- The search field stays put and only what it finds scrolls, as in a search sheet on iOS. -->
    <div class="pick" data-fills-dialog>
      <SearchField
        id="diary-food-search"
        value={query}
        placeholder={m['diary.add.searchPlaceholder']()}
        label={m['diary.add.foods']()}
        clearLabel={m['app.dismiss']()}
        oninput={(text) => void search(text)}
        onclear={() => void search('')}
      />

      <div class="results">
        {#if looking}
          {#if found.length > 0}
            <ListSection title={m['diary.add.foods']()} level="h3">
              {#each found as food (food.code)}
                <ListRow onclick={() => onfood(food)}>
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
                  {#snippet trailing()}<Chevron />{/snippet}
                </ListRow>
              {/each}
            </ListSection>
          {:else if answered === query}
            <p class="note">{m['diary.add.nothingFound']({ query: query.trim() })}</p>
          {/if}
        {/if}

        <!-- The other ways in, for what the table cannot find: a recipe of the household, or one's own figures. Under
             the results while searching, so a food that is not there leads straight on to them. -->
        <ul class="ways">
          <ListRow onclick={onrecipe}>
            {m['diary.add.recipe']()}
            {#snippet trailing()}<Chevron />{/snippet}
          </ListRow>
          <ListRow onclick={() => (view = 'own')}>
            {m['diary.add.own']()}
            {#snippet trailing()}<Chevron />{/snippet}
          </ListRow>
        </ul>

        {#if !looking}
          {#if recent.length === 0}
            <p class="note">{m['diary.add.recentEmpty']()}</p>
          {:else}
            <ListSection title={m['diary.add.recent']()} level="h3">
              {#each recent as entry (entry.entryId)}
                <ListRow
                  label={m['diary.add.again']({ label: entry.label })}
                  onclick={() => again(entry)}
                >
                  <span class="name">{entry.label}</span>
                  <span class="detail">
                    {[
                      amountText(entry),
                      entry.figures.kcal == null ? null : kcalText(entry.figures.kcal, entry)
                    ]
                      .filter(Boolean)
                      .join(' · ')}
                  </span>
                  {#snippet trailing()}{@render plus()}{/snippet}
                </ListRow>
              {/each}
            </ListSection>
          {/if}
        {/if}
      </div>
    </div>
  {/if}
</Sheet>

<style>
  .pick {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
  }

  /*
   * Only the results scroll. They fade out under the search field instead of meeting it at a hard line (Apple's
   * scroll-edge effect); the padding keeps the first card clear of the fade until something scrolls into it.
   */
  .results {
    display: flex;
    flex-direction: column;
    gap: var(--space-6);
    min-block-size: 0;
    /* Into the sheet's bottom padding, so the list runs to the sheet's edge instead of stopping above it. */
    margin-block-end: calc(-1 * var(--space-6));
    padding-block: var(--space-3) var(--space-6);
    overflow-y: auto;
    overscroll-behavior: contain;
    scrollbar-width: none;
    /* A mask reads only alpha: any opaque colour shows the content, and the text colour is always opaque. */
    mask-image: linear-gradient(to bottom, transparent, var(--text) var(--space-3));
  }

  .results::-webkit-scrollbar {
    display: none;
  }

  /* A card clips its rows, so in a flex column it would shrink to fit and hide them; the results scroll instead. */
  .results > :global(*) {
    flex-shrink: 0;
  }

  /* The ways in, a card of their own with no heading: they are actions, not a list of things. */
  .ways {
    overflow: hidden;
    margin: 0;
    padding: 0;
    border-radius: var(--radius-lg);
    background: var(--surface-raised);
    list-style: none;
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
    padding-inline: var(--space-4);
    text-wrap: pretty;
  }

  /* The quick add's mark: one tap logs it again, with the amount of last time. */
  .plus {
    display: block;
    inline-size: var(--space-6);
    block-size: var(--space-6);
    padding: var(--space-1);
    border-radius: var(--radius-full);
    background: var(--surface-accent-subtle);
    color: var(--accent);
  }

  .own {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
  }

  .back {
    display: inline-flex;
    align-items: center;
    align-self: flex-start;
    gap: var(--space-1);
    min-block-size: var(--control-sm);
    margin-inline-start: calc(-1 * var(--space-2));
    padding: 0 var(--space-2);
    border: 0;
    border-radius: var(--radius-md);
    background: transparent;
    color: var(--accent);
    font: inherit;
    font-weight: var(--weight-medium);
    cursor: pointer;
  }

  .back:focus-visible {
    outline: 2px solid var(--border-focus);
  }

  .own-title {
    margin: 0;
    font-size: var(--text-lg);
  }

  .own .note {
    padding-inline: 0;
  }
</style>
