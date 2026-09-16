<script lang="ts">
  import { untrack } from 'svelte';
  import { resolve } from '$app/paths';
  import { Button, EmptyState, ErrorState, FilterChip, SearchField } from '$ds';
  import FeaturedRecipe from '$features/recipes/FeaturedRecipe.svelte';
  import RecipeGrid from '$features/recipes/RecipeGrid.svelte';
  import { recipes } from '$features/recipes/stores/recipes.svelte';
  import { libraryView } from '$features/recipes/stores/libraryView.svelte';
  import PageHeader from '$shell/PageHeader.svelte';
  import { session } from '$features/auth/session.svelte';
  import { m } from '$shell/i18n';
  import Page from '$shell/Page.svelte';

  /**
   * Everything the household can cook.
   *
   * The search is debounced rather than fired per keystroke, and a refetch
   * keeps the list that is already on screen — the old answer is almost always
   * still the right one, and replacing it with a skeleton loses your place.
   */
  let search = $state(untrack(() => libraryView.query));
  const applied = $derived(libraryView.query);

  let debounce: ReturnType<typeof setTimeout> | undefined;

  const householdId = $derived(session.activeHouseholdId);

  /** Empty because of a filter is a mistake to undo; empty because it is new is an invitation. */
  const filtered = $derived(applied.trim().length > 0 || libraryView.quick);

  /** A photographed recipe gives the unfiltered library a visual starting point. */
  const featured = $derived(
    !filtered ? recipes.items.find((recipe) => recipe.imageId !== null) : undefined
  );
  const library = $derived(
    featured ? recipes.items.filter((recipe) => recipe.id !== featured.id) : recipes.items
  );

  /**
   * Whether the end of the list fetches the next page by itself.
   *
   * It stops once a page fails. A list that asks for itself would otherwise
   * ask forever while the connection is down, because the thing that triggers
   * the request — the end of the list, in view — never goes away. From then on
   * it is a button, and one deliberate press is worth more than a thousand.
   */
  const autoLoads = $derived(recipes.status === 'ready' && recipes.hasMore && !recipes.moreFailed);

  $effect(() => {
    if (householdId) {
      untrack(() => {
        clearTimeout(debounce);
        libraryView.forHousehold(householdId);
        search = libraryView.query;
      });
    }
  });

  $effect(() => {
    if (householdId) {
      void recipes.list(householdId, {
        query: applied,
        maxMinutes: libraryView.quick ? 30 : undefined
      });
    }
  });

  $effect(() => () => clearTimeout(debounce));

  function type(value: string) {
    search = value;
    clearTimeout(debounce);

    // Long enough that a word is finished, short enough that it feels live.
    debounce = setTimeout(() => (libraryView.query = value), 250);
  }

  function clear() {
    search = '';
    clearTimeout(debounce);
    libraryView.query = '';
  }

  function resetFilters() {
    clear();
    libraryView.quick = false;
  }

  function retry() {
    recipes.clearError();

    if (householdId) {
      void recipes.list(householdId, {
        query: applied,
        maxMinutes: libraryView.quick ? 30 : undefined
      });
    }
  }

  /**
   * The next page, asked for by reading far enough down.
   *
   * Also the retry: a page that failed is asked for in exactly the same way,
   * by the same call, so there is no second path that can drift.
   */
  function more() {
    if (householdId) {
      void recipes.loadMore(householdId, {
        query: applied,
        maxMinutes: libraryView.quick ? 30 : undefined
      });
    }
  }
</script>

<svelte:head><title>{m['recipes.title']()}</title></svelte:head>

<Page>
  <PageHeader title={m['recipes.title']()} subtitle={m['recipes.collection.subtitle']()} />

  <div class="collection-toolbar">
    <div class="collection-tools">
      <div class="search">
        <SearchField
          id="recipe-search"
          value={search}
          label={m['recipes.list.searchLabel']()}
          placeholder={m['recipes.list.searchPlaceholder']()}
          clearLabel={m['recipes.list.clearSearch']()}
          oninput={type}
          onclear={clear}
        />
      </div>
      <FilterChip
        shape="rounded"
        selected={libraryView.quick}
        onclick={() => (libraryView.quick = !libraryView.quick)}
      >
        {#snippet icon()}
          <svg
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="1.8"
            aria-hidden="true"
          >
            <circle cx="12" cy="12" r="9" /><path d="M12 7v5l3 2" stroke-linecap="round" />
          </svg>
        {/snippet}
        {m['recipes.filter.quick']()}
      </FilterChip>
    </div>

    <div class="collection-summary" aria-live="polite" aria-atomic="true">
      <p class="count">
        {#if recipes.status === 'loading' || recipes.status === 'idle'}
          {m['recipes.list.loading']()}
        {:else if recipes.status === 'ready'}
          {m['recipes.list.count']({ count: recipes.total })}
        {/if}
      </p>
      {#if filtered}
        <Button size="sm" variant="ghost" onclick={resetFilters}
          >{m['recipes.filter.reset']()}</Button
        >
      {:else if recipes.status === 'ready' && recipes.items.length > 0}
        <p class="collection-note">{m['recipes.list.recent']()}</p>
      {/if}
    </div>
  </div>

  {#if recipes.status === 'failed'}
    <ErrorState
      title={m['recipes.failed.title']()}
      body={m['recipes.failed.body']()}
      requestIdLabel={m['error.reference']()}
      requestId={recipes.error?.requestId}
    >
      {#snippet action()}
        <Button variant="primary" onclick={retry}>{m['error.retry']()}</Button>
      {/snippet}
    </ErrorState>
  {:else if recipes.status === 'ready' && recipes.items.length === 0 && filtered}
    <EmptyState title={m['recipes.filtered.title']()} body={m['recipes.filtered.body']()}>
      {#snippet action()}
        <Button onclick={resetFilters}>{m['recipes.filtered.action']()}</Button>
      {/snippet}
    </EmptyState>
  {:else if recipes.status === 'ready' && recipes.items.length === 0}
    <EmptyState title={m['recipes.empty.title']()} body={m['recipes.empty.body']()}>
      {#snippet icon()}
        <svg
          viewBox="0 0 48 48"
          fill="none"
          stroke="currentColor"
          stroke-width="1.5"
          aria-hidden="true"
        >
          <path
            d="M24 12c-5-4-12-5-18-3v28c6-2 13-1 18 3 5-4 12-5 18-3V9c-6-2-13-1-18 3Zm0 0v28M12 17l6 1m-6 6 6 1m12-7 6-1m-6 8 6-1"
            stroke-linecap="round"
            stroke-linejoin="round"
          />
        </svg>
      {/snippet}
      {#snippet action()}
        <Button variant="primary" href={resolve('/(app)/recipes/new')}>
          {m['recipes.empty.action']()}
        </Button>
      {/snippet}
    </EmptyState>
  {:else}
    {#if featured}
      <FeaturedRecipe recipe={featured} />
    {/if}

    <RecipeGrid
      recipes={library}
      loading={recipes.status === 'loading' && recipes.items.length === 0}
      onmore={autoLoads ? more : undefined}
    />

    {#if recipes.moreFailed}
      <div class="more">
        <p class="stalled">{m['recipes.list.moreFailed']()}</p>
        <Button onclick={more}>{m['error.retry']()}</Button>
      </div>
    {/if}
  {/if}
</Page>

<style>
  .collection-toolbar {
    display: flex;
    align-items: center;
    flex-wrap: wrap;
    gap: var(--space-3) var(--space-6);
    padding-block: var(--space-4);
    margin-bottom: var(--space-6);
    border-block: 1px solid var(--border);
  }
  .collection-tools {
    display: flex;
    align-items: center;
    flex-wrap: wrap;
    gap: var(--space-2);
    flex: 1 1 28rem;
    min-width: 0;
  }
  .search {
    flex: 1 1 14rem;
    max-width: 28rem;
    min-width: 0;
  }
  .collection-summary {
    display: flex;
    flex-direction: column;
    align-items: flex-end;
    gap: var(--space-1);
    margin-inline-start: auto;
    color: var(--text-muted);
    font-size: var(--text-sm);
  }
  .count {
    color: var(--text);
    font-weight: var(--weight-medium);
    font-variant-numeric: tabular-nums;
  }
  .collection-note {
    font-size: var(--text-xs);
  }
  @media (max-width: 40rem) {
    .search {
      flex-basis: 100%;
      max-width: none;
    }
    .collection-summary {
      width: 100%;
      flex-direction: row;
      align-items: center;
      justify-content: space-between;
    }
  }

  .more {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: var(--space-3);
    margin-top: var(--space-8);
  }

  .stalled {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }
</style>
