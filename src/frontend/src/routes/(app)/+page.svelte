<script lang="ts">
  import { untrack } from 'svelte';
  import { resolve } from '$app/paths';
  import { Button, EmptyState, ErrorState } from '$ds';
  import RecipeGrid from '$features/recipes/RecipeGrid.svelte';
  import SuggestionDeck from '$features/recipes/SuggestionDeck.svelte';
  import LibraryToolbar from '$features/recipes/filters/LibraryToolbar.svelte';
  import { sortLabel } from '$features/recipes/filters/labels';
  import { recipes } from '$features/recipes/stores/recipes.svelte';
  import { effectiveSort, libraryView } from '$features/recipes/stores/libraryView.svelte';
  import { recallRanking, rememberRanking } from '$features/recipes/stores/rankingHint';
  import type { SavedSearch } from '$features/recipes/stores/savedSearches.svelte';
  import CookbookSheet from '$features/cookbooks/CookbookSheet.svelte';
  import { cookbooks } from '$features/cookbooks/stores/cookbooks.svelte';
  import type { CookbookRules } from '$features/cookbooks/types';
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
   * The toolbar owns the search box, the filters and the debounce; this page
   * owns what to do with the answer. A refetch keeps the list that is already
   * on screen — the old answer is almost always still the right one, and
   * replacing it with a skeleton loses your place.
   */
  const householdId = $derived(session.activeHouseholdId);

  /** The shelf a saved search has proposed, while its sheet is open. */
  let shelving = $state<{ name: string; rules: CookbookRules } | null>(null);
  let shelvingBusy = $state(false);

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
  const filtered = $derived(libraryView.filtered);

  /** The answer to "what should I cook?", best first, whatever the page is showing. */
  const shortlist = $derived(suggestions.for(householdId, featuredQuery));

  const topSuggestion = $derived(shortlist[0]);

  /**
   * What the ranking said about this kitchen the last time this device asked.
   *
   * Read once per household and then held for the visit, deliberately: it is
   * not replaced when the fresh answer arrives. A list fetched in one order and
   * re-fetched in another is a page that rearranges itself under somebody who
   * has started reading it. When the two disagree — about once in the life of a
   * kitchen — this visit keeps the order it began with, and the next one has
   * the new answer.
   */
  const remembered = $derived(householdId ? recallRanking(householdId) : null);

  /**
   * Whether the ranking has anything true to say about this kitchen yet.
   *
   * Used instead of counting cook-log entries against a threshold, and it is
   * the better signal: a reason exists exactly when one term of the score
   * actually dominated, which is the same thing as "there is enough history
   * here for the order to mean something". A brand-new kitchen gets the app it
   * has always had, and nothing had to guess a number.
   */
  const ranks = $derived(remembered ?? topSuggestion?.reason != null);

  const searching = $derived(libraryView.query.trim().length > 0);

  /**
   * What the toolbar needs in order to decide an order nobody has chosen.
   *
   * `ranks` is the same signal the suggested chip used: the ranking may only
   * take over once it has something true to say about this kitchen.
   */
  const context = $derived({ searching, ranks, inACookbook: false });

  const order = $derived(effectiveSort(libraryView.sort, context));

  /**
   * Whether the order is settled enough to ask for a list in it.
   *
   * The question is only open when nobody has chosen an order and nothing has
   * been typed: then, and only then, the order depends on what the ranking
   * says. Anywhere else waiting for the shortlist was a round trip spent on an
   * answer that could not change anything.
   *
   * When it does depend, a remembered answer settles it at once, and the list
   * is asked for alongside the shortlist rather than after it. With nothing
   * remembered — a first visit on this device — the list waits, because
   * listing first and re-listing when the answer arrives is the page
   * rearranging itself under somebody who is already reading it. One small
   * request first is the cheaper of those two costs, and the skeleton was
   * already going to be on screen for it.
   *
   * A failed suggestion counts as settled — it means "recently updated", which
   * is the order the app has always had.
   */
  const settled = $derived(
    !householdId ||
      libraryView.sort !== null ||
      searching ||
      remembered !== null ||
      suggestions.answered(householdId, featuredQuery)
  );

  /**
   * Which list is on screen.
   *
   * Built once, because the first page, the next page and the retry all have to
   * ask for the same thing — three copies of this object is three ways for a
   * "load more" to append rows from a different list than the one above it.
   */
  const filters = $derived({
    query: libraryView.query,
    tags: libraryView.tags,
    maxMinutes: libraryView.maxMinutes ?? undefined,
    // Always explicit, so that the order the page names above the grid is the
    // order it actually asked for. The server would pick the same one from an
    // absent `sort`, but a label worked out separately from the request is a
    // label that can be wrong.
    sort: order
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
      untrack(() => libraryView.forHousehold(householdId));
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

  // Kept for the next visit, and only from an answer that actually came back:
  // a failed shortlist says nothing about the kitchen, and remembering it as
  // "nothing to say" would hold a ranked kitchen in recent order until the
  // next success.
  $effect(() => {
    if (householdId && suggestions.statusOf(householdId, featuredQuery) === 'ready') {
      rememberRanking(householdId, topSuggestion?.reason != null);
    }
  });

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
    libraryView.clear();
  }

  /**
   * A saved search, made into a shelf.
   *
   * The two are different things — a search is a lens, ordered and fuzzy; a
   * shelf is a curation that can be counted, drawn and taken to the shop — and
   * this is the one door between them. Only what a shelf can actually ask for
   * crosses: the tags and the time limit. The words stay behind, and the sheet
   * says so rather than quietly dropping them.
   */
  function promote(search: SavedSearch) {
    shelving = {
      name: search.name,
      rules: { tags: [...search.tags], ingredients: [], maxMinutes: search.maxMinutes }
    };
  }

  async function makeCookbook(
    name: string,
    description: string | null,
    rules: CookbookRules | null
  ) {
    if (!householdId || !rules) {
      return;
    }

    shelvingBusy = true;

    const made = await cookbooks.create(householdId, name, description ?? undefined, rules);

    shelvingBusy = false;

    if (made) {
      shelving = null;
      toaster.show({ message: m['saved.promoted']({ name }) });

      return;
    }

    toaster.show({ message: m['cookbooks.add.failed'](), tone: 'danger' });
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

  <LibraryToolbar
    id="recipe-search"
    householdId={householdId ?? ''}
    view={libraryView}
    {context}
    searchLabel={m['recipes.list.searchLabel']()}
    searchPlaceholder={m['recipes.list.searchPlaceholder']()}
    savable
    onpromote={promote}
  >
    {#snippet summary()}
      <div class="collection-summary" aria-live="polite" aria-atomic="true">
        <p class="count">
          {#if !settled || recipes.status === 'loading' || recipes.status === 'idle'}
            {m['recipes.list.loading']()}
          {:else if recipes.status === 'ready'}
            {m['recipes.list.count']({ count: recipes.total })}
          {/if}
        </p>
        {#if filtered}
          <Button size="sm" variant="ghost" onclick={resetFilters}>
            {m['recipes.filter.reset']()}
          </Button>
        {:else if recipes.status === 'ready' && recipes.items.length > 0}
          <!-- The order is always named. A list whose order changed without
               saying so is the thing that makes people stop trusting an app. -->
          <p class="collection-note">{sortLabel(order)}</p>
        {/if}
      </div>
    {/snippet}
  </LibraryToolbar>

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

<!-- A saved search, offered as a shelf. The same sheet the cookbooks page
     uses, so a cookbook made this way is made exactly like every other one. -->
<CookbookSheet
  open={shelving !== null}
  householdId={householdId ?? ''}
  preset={shelving}
  saving={shelvingBusy}
  onsave={(name, description, rules) => void makeCookbook(name, description, rules)}
  onclose={() => (shelving = null)}
/>

<style>
  .collection-summary {
    display: flex;
    align-items: center;
    gap: var(--space-3);
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
    .collection-summary {
      width: 100%;
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
