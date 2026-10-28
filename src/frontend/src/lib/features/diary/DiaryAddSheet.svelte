<script lang="ts">
  import { Button, Chevron, IconButton, ListRow, ListSection, SearchField, Sheet } from '$ds';
  import { session } from '$features/auth/session.svelte';
  import { searchFoods } from '$features/nutrition/stores/foods';
  import { householdFoods } from '$features/nutrition/stores/householdFoods.svelte';
  import { foldText } from '$features/recipes/foldText';
  import { createRecipeStore } from '$features/recipes/stores/recipes.svelte';
  import type { RecipeSummary } from '$features/recipes/types';
  import {
    productTitle,
    searchCatalogue,
    type CatalogueProduct
  } from '$features/scanning/catalogue';
  import { explain } from '$shell/explain';
  import { m } from '$shell/i18n';

  import type { AmountSubject } from './DiaryAmountSheet.svelte';
  import DiaryOwnFigures, {
    noFigures,
    typedFigures,
    type TypedFigures
  } from './DiaryOwnFigures.svelte';
  import DiaryProduct from './DiaryProduct.svelte';
  import { amountText, kcalText, mealLabel } from './format';
  import { fitting } from './matching';
  import { diary } from './stores/diary.svelte';
  import type { DiaryEntry, DiaryMeal } from './types';

  /**
   * What was eaten at one meal, the quick way first (docs/diary-design.md §6, §9.4): what this person ate before
   * (one tap), a recipe of the household, a food from the table or the household's own (then an amount), a scanned
   * package, or their own figures for a café lunch. Typing searches all of them at once, in that order; Open Food
   * Facts only when asked, since it allows a few searches a minute. Figures are always the server's, except the
   * person's own.
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
    /** A recipe the search found, chosen straight from the list. */
    onpickrecipe: (recipe: RecipeSummary) => void;
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
    onpickrecipe,
    onfood,
    ontyped,
    onclose
  }: Props = $props();

  /** The sheet's one path (Apple's "single path through subviews"): pick something, type figures, or a package. */
  let view = $state<'pick' | 'own' | 'product'>('pick');
  /** The package to show in the product view: one chosen from Open Food Facts, or null to scan. */
  let productGtin = $state<string | null>(null);

  const openFoodFacts = $derived(session.user?.openFoodFacts ?? false);
  const recent = $derived(diary.recent(8, meal));
  let query = $state('');
  let table = $state<FoodChoice[]>([]);
  /** The query the results in `table` answer, so "nothing found" is said only once the search has come back. */
  let answered = $state('');
  let searching = 0;
  let debounce: ReturnType<typeof setTimeout> | undefined;

  /** The household's recipes that fit, from a list of the sheet's own (not the library's, which is behind it). */
  const recipeSearch = createRecipeStore();

  /** What Open Food Facts found for one query, asked for by the person. */
  let catalogue = $state<{
    readonly query: string;
    readonly status: 'asking' | 'ready' | 'failed';
    readonly products: readonly CatalogueProduct[];
    readonly failure: string | null;
  } | null>(null);

  let own = $state(noFigures());

  /** Typing turns the sheet from what was eaten before into what everything holds. */
  const looking = $derived(foldText(query.trim()).length >= 2);

  $effect(() => {
    if (open) {
      view = 'pick';
      productGtin = null;
      if (householdId) void householdFoods.load(householdId);
    }
  });

  $effect(() => () => clearTimeout(debounce));

  /** Eaten before and fitting what was typed: the likeliest answer, and one tap. */
  const eatenBefore = $derived(
    looking ? fitting(diary.recent(60), (entry) => entry.label, query).slice(0, 3) : []
  );

  const ownFoods = $derived<FoodChoice[]>(
    looking
      ? fitting(householdFoods.foods, (food) => food.name, query).map((food) => ({
          code: food.code,
          householdId,
          subject: { kind: 'food', name: food.name, kcalPer100Grams: food.energyKcal, units: [] }
        }))
      : []
  );

  /** The household's own foods first (what is in the cupboard), then the table's. */
  const foods = $derived([...ownFoods, ...table]);

  // By title only: the library's search also ranks by ingredients, so "Milch" would bring every béchamel.
  const recipesFound = $derived(
    looking && recipeSearch.status === 'ready'
      ? fitting(recipeSearch.items, (recipe) => recipe.title, query).slice(0, 3)
      : []
  );

  function search(text: string) {
    query = text;
    clearTimeout(debounce);

    if (foldText(text.trim()).length < 2) {
      table = [];
      answered = '';
      searching++;

      return;
    }

    // The table and the recipes are asked once typing pauses; what the sheet holds itself is filtered at once.
    debounce = setTimeout(() => void ask(text), 150);
  }

  async function ask(text: string) {
    const asked = ++searching;

    if (householdId) void recipeSearch.list(householdId, { query: text.trim() });

    const found = await searchFoods(text.trim(), 20);

    if (asked !== searching) return;

    table = found.ok
      ? found.foods.map((food) => ({
          code: food.code,
          householdId: null,
          subject: {
            kind: 'food' as const,
            name: food.nameDe,
            kcalPer100Grams: food.energyKcal,
            units: food.units ?? []
          }
        }))
      : [];
    answered = text;
  }

  /** One search at Open Food Facts, for what nothing here has: never while typing, since it allows a few a minute. */
  async function askCatalogue() {
    const text = query.trim();

    catalogue = { query: text, status: 'asking', products: [], failure: null };

    const found = await searchCatalogue(text);

    if (catalogue?.query !== text) return;

    catalogue = found.ok
      ? { query: text, status: 'ready', products: found.value, failure: null }
      : { query: text, status: 'failed', products: [], failure: explain(found.error) };
  }

  function scan(gtin: string | null = null) {
    productGtin = gtin;
    view = 'product';
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

  const nothing = $derived(
    looking &&
      answered === query &&
      foods.length === 0 &&
      eatenBefore.length === 0 &&
      recipesFound.length === 0
  );
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

{#snippet barcode()}
  <svg
    viewBox="0 0 24 24"
    fill="none"
    stroke="currentColor"
    stroke-width="1.8"
    stroke-linecap="round"
    aria-hidden="true"
  >
    <path
      d="M4 7V5a1 1 0 0 1 1-1h2M17 4h2a1 1 0 0 1 1 1v2M20 17v2a1 1 0 0 1-1 1h-2M7 20H5a1 1 0 0 1-1-1v-2"
    />
    <path d="M8 8v8M11 8v8M14 8v8M17 8v8" />
  </svg>
{/snippet}

{#snippet foodDetail(food: FoodChoice)}
  <span class="detail">
    {#if food.subject.kcalPer100Grams != null}
      {m['diary.add.per100']({
        kcal: kcalText(food.subject.kcalPer100Grams, { atLeast: false, estimated: false })
      })}
    {/if}
    {#if food.householdId}· {m['diary.add.ownFood']()}{/if}
  </span>
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
  {:else if view === 'product' && householdId}
    <DiaryProduct
      {householdId}
      {openFoodFacts}
      gtin={productGtin}
      {onfood}
      onback={() => (view = 'pick')}
    />
  {:else}
    <!-- The search field stays put and only what it finds scrolls, as in a search sheet on iOS. -->
    <div class="pick" data-fills-dialog>
      <div class="search">
        <SearchField
          id="diary-food-search"
          value={query}
          placeholder={m['diary.add.searchPlaceholder']()}
          label={m['diary.add.foods']()}
          clearLabel={m['app.dismiss']()}
          oninput={search}
          onclear={() => search('')}
        />
        {#if householdId}
          <IconButton label={m['diary.add.scan']()} onclick={() => scan()}>
            {@render barcode()}
          </IconButton>
        {/if}
      </div>

      <div class="results">
        {#if looking}
          {#if eatenBefore.length > 0}
            <ListSection title={m['diary.add.eatenBefore']()} level="h3">
              {#each eatenBefore as entry (entry.entryId)}
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

          {#if recipesFound.length > 0}
            <ListSection title={m['diary.add.recipes']()} level="h3">
              {#each recipesFound as recipe (recipe.id)}
                <ListRow onclick={() => onpickrecipe(recipe)}>
                  <span class="name">{recipe.title}</span>
                  {#snippet trailing()}<Chevron />{/snippet}
                </ListRow>
              {/each}
            </ListSection>
          {/if}

          {#if foods.length > 0}
            <ListSection title={m['diary.add.foods']()} level="h3">
              {#each foods as food (food.code)}
                <ListRow onclick={() => onfood(food)}>
                  <span class="name">{food.subject.name}</span>
                  {@render foodDetail(food)}
                  {#snippet trailing()}<Chevron />{/snippet}
                </ListRow>
              {/each}
            </ListSection>
          {:else if nothing}
            <p class="note">{m['diary.add.nothingFound']({ query: query.trim() })}</p>
          {/if}

          <!-- Open Food Facts on request only: one query leaves the server, and only when asked. -->
          {#if openFoodFacts && householdId}
            {#if catalogue?.query === query.trim() && catalogue.status !== 'asking'}
              {#if catalogue.failure}
                <p class="note" role="status">{catalogue.failure}</p>
              {:else if catalogue.products.length === 0}
                <p class="note" role="status">
                  {m['diary.add.catalogueEmpty']({ query: catalogue.query })}
                </p>
              {:else}
                <ListSection title={m['diary.add.catalogueTitle']()} level="h3">
                  {#each catalogue.products as product (product.gtin)}
                    <ListRow onclick={() => scan(product.gtin)}>
                      <span class="name">{productTitle(product)}</span>
                      <span class="detail">
                        {[
                          product.quantity,
                          product.doubts.includes('noEnergy')
                            ? null
                            : m['diary.add.per100']({
                                kcal: kcalText(product.label.energyKcal, {
                                  atLeast: false,
                                  estimated: false
                                })
                              })
                        ]
                          .filter(Boolean)
                          .join(' · ')}
                      </span>
                      {#snippet trailing()}<Chevron />{/snippet}
                    </ListRow>
                  {/each}
                </ListSection>
                <p class="note">{m['diary.add.catalogueSource']()}</p>
              {/if}
            {:else}
              <div class="catalogue">
                <Button
                  size="sm"
                  loading={catalogue?.status === 'asking'}
                  onclick={() => void askCatalogue()}
                >
                  {m['diary.add.catalogue']()}
                </Button>
                <p class="note">{m['diary.add.catalogueHint']()}</p>
              </div>
            {/if}
          {/if}
        {/if}

        <!-- The other ways in, for what the search cannot find: a package, a recipe of the household, or one's own
             figures. Under the results while searching, so a food that is not there leads straight on to them. -->
        <ul class="ways">
          {#if householdId}
            <ListRow onclick={() => scan()}>
              {m['diary.add.scanRow']()}
              {#snippet trailing()}<Chevron />{/snippet}
            </ListRow>
          {/if}
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

  .search {
    display: flex;
    align-items: center;
    gap: var(--space-2);
  }

  .search > :global(:first-child) {
    flex: 1;
    min-inline-size: 0;
  }

  .catalogue {
    display: flex;
    flex-direction: column;
    align-items: flex-start;
    gap: var(--space-2);
    padding-inline: var(--space-4);
  }

  .catalogue .note {
    padding-inline: 0;
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
