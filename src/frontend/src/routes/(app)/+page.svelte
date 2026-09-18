<script lang="ts">
  import { untrack } from 'svelte';
  import { resolve } from '$app/paths';
  import { Button, EmptyState, ErrorState, FilterChip, SearchField } from '$ds';
  import RecipeGrid from '$features/recipes/RecipeGrid.svelte';
  import SuggestionDeck from '$features/recipes/SuggestionDeck.svelte';
  import { recipes } from '$features/recipes/stores/recipes.svelte';
  import { libraryView } from '$features/recipes/stores/libraryView.svelte';
  import { suggestions } from '$features/recipes/stores/suggestions.svelte';
  import type { Suggestion } from '$features/recipes/types';
  import PageHeader from '$shell/PageHeader.svelte';
  import { session } from '$features/auth/session.svelte';
  import { m } from '$shell/i18n';
  import { toaster } from '$shell/toaster.svelte';
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

  /**
   * The shortlist at the top of the page. Asked once per household.
   *
   * Five, which is the server's own default and about as many answers as
   * anybody holds in their head while deciding what to eat. It was three when
   * the panel showed one and the other two were only there so that dismissing
   * the leader revealed the next instead of emptying it; now every one of them
   * is walked past, and a shortlist you reach the end of in two swipes is not
   * one. Twelve is the ceiling and would be a feed.
   */
  const featuredQuery = { limit: 5 } as const;

  /** Empty because of a filter is a mistake to undo; empty because it is new is an invitation. */
  const filtered = $derived(applied.trim().length > 0 || libraryView.quick);

  /** The answer to "what should I cook?", best first, whatever the page is showing. */
  const shortlist = $derived(suggestions.for(householdId, featuredQuery));

  const topSuggestion = $derived(shortlist[0]);

  /**
   * Whether the ranking has anything true to say about this kitchen yet.
   *
   * Used instead of counting cook-log entries against a threshold, and it is
   * the better signal: a reason exists exactly when one term of the score
   * actually dominated, which is the same thing as "there is enough history
   * here for the order to mean something". A brand-new kitchen gets the app it
   * has always had, and nothing had to guess a number.
   */
  const ranks = $derived(topSuggestion?.reason != null);

  const order = $derived(libraryView.order ?? (ranks ? 'suggested' : 'recent'));

  /**
   * Whether the order is settled enough to ask for a list in it.
   *
   * The list is not fetched until the suggestion question has come back, either
   * way. Listing first and re-listing when the answer arrives works, and it is
   * wrong: the page settles, somebody starts reading it, and then it silently
   * rearranges itself under them. One small request first is the cheaper of the
   * two costs, and the skeleton was already going to be on screen for it.
   *
   * A failed suggestion counts as settled — it means "recently updated", which
   * is the order the app has always had.
   */
  const settled = $derived(!householdId || suggestions.answered(householdId, featuredQuery));

  /**
   * Which list is on screen.
   *
   * Built once, because the first page, the next page and the retry all have to
   * ask for the same thing — three copies of this object is three ways for a
   * "load more" to append rows from a different list than the one above it.
   */
  const filters = $derived({
    query: applied,
    maxMinutes: libraryView.quick ? 30 : undefined,
    sort: order === 'suggested' ? ('suggested' as const) : undefined
  });

  /**
   * What leads the page.
   *
   * The panel has always been here; what filled it was the first recipe with a
   * photograph, which is an accident rather than an answer. Then it was the
   * best suggestion, with the reason as its eyebrow. Now it is the whole
   * shortlist, one at a time, because "not tonight, what else?" is the ordinary
   * reply to a suggestion and the only way to say it was to say "never again".
   *
   * The photograph is still the fallback, and it is handed over as a suggestion
   * with no reason — which is exactly what it is: something shown with nothing
   * to say about why. The deck then draws it as the panel has always looked,
   * with the fixed eyebrow and nothing to walk.
   */
  const fallback = $derived(
    filtered || shortlist.length > 0
      ? undefined
      : recipes.items.find((recipe) => recipe.imageId !== null)
  );

  const lead = $derived<readonly Suggestion[]>(
    filtered
      ? []
      : shortlist.length > 0
        ? shortlist
        : fallback
          ? [{ ...fallback, reason: null }]
          : []
  );

  /**
   * The grid, minus everything the panel is holding.
   *
   * The whole shortlist, not just the one on screen. Hiding only the visible
   * panel was right when the panel could not change; now a swipe would push one
   * recipe into the grid and pull another out of it, and the page would
   * rearrange itself below the thumb every time somebody looked at the next
   * idea. A set chosen once is a grid that sits still.
   */
  const library = $derived(
    lead.length > 0
      ? recipes.items.filter((recipe) => !lead.some((one) => one.id === recipe.id))
      : recipes.items
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
      void suggestions.ask(householdId, featuredQuery);
    }
  });

  $effect(() => {
    if (householdId && settled) {
      void recipes.list(householdId, filters);
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

  /**
   * "Not this one."
   *
   * Optimistic, and answered with an Undo toast rather than a confirmation —
   * the same shape as moving a planned meal, because a dismissal is a small
   * reversible decision and a dialog would make it feel like a large one. The
   * panel refills from the next answer rather than jumping to whatever was
   * second, so nothing moves under the thumb that just tapped.
   */
  async function hide(recipeId: string) {
    const failure = await suggestions.dismiss(recipeId);

    if (failure) {
      toaster.show({ message: m['suggestions.dismissFailed'](), tone: 'danger' });

      return;
    }

    toaster.show({
      message: m['suggestions.dismissed'](),
      tone: 'success',
      action: {
        label: m['suggestions.restore'](),
        run: () => void restore(recipeId)
      }
    });
  }

  async function restore(recipeId: string) {
    await suggestions.restore(recipeId);

    if (householdId) {
      void suggestions.ask(householdId, featuredQuery);
    }
  }

  function resetFilters() {
    clear();
    libraryView.quick = false;
  }

  function retry() {
    recipes.clearError();

    if (householdId) {
      void recipes.list(householdId, filters);
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
      void recipes.loadMore(householdId, filters);
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
      {#if ranks}
        <!-- Only once the ranking has something true to say. Before that the
             choice would be between one real order and one that is not yet an
             order at all. -->
        <FilterChip
          shape="rounded"
          selected={order === 'suggested'}
          onclick={() => (libraryView.order = order === 'suggested' ? 'recent' : 'suggested')}
        >
          {#snippet icon()}
            <svg
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="1.8"
              aria-hidden="true"
            >
              <path
                d="M12 3.6 14.3 9l5.7.4-4.4 3.7 1.4 5.6L12 15.8 7 18.7l1.4-5.6L4 9.4 9.7 9Z"
                stroke-linejoin="round"
              />
            </svg>
          {/snippet}
          {m['recipes.sort.suggested']()}
        </FilterChip>
      {/if}

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
        {#if !settled || recipes.status === 'loading' || recipes.status === 'idle'}
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
        <!-- The order is always named. A list whose order changed without
             saying so is the thing that makes people stop trusting an app. -->
        <p class="collection-note">
          {order === 'suggested' ? m['recipes.list.suggested']() : m['recipes.list.recent']()}
        </p>
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
    {#if lead.length > 0}
      <SuggestionDeck
        items={lead}
        ondismiss={shortlist.length > 0 ? (recipeId) => void hide(recipeId) : undefined}
      />
    {/if}

    <RecipeGrid
      recipes={library}
      loading={!settled || (recipes.status === 'loading' && recipes.items.length === 0)}
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
