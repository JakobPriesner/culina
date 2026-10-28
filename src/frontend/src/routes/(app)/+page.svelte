<script lang="ts">
  import { untrack } from 'svelte';

  import { goto } from '$app/navigation';
  import { resolve } from '$app/paths';
  import { page } from '$app/state';
  import { Button, EmptyState, ErrorState } from '$ds';
  import { session } from '$features/auth/session.svelte';
  import TodayMeal from '$features/planning/TodayMeal.svelte';
  import EmptyLibrary from '$features/recipes/library/EmptyLibrary.svelte';
  import { focusSearchOnFind } from '$features/recipes/library/focusSearchOnFind';
  import LibrarySummary from '$features/recipes/library/LibrarySummary.svelte';
  import RefreshFailed from '$features/recipes/library/RefreshFailed.svelte';
  import PromoteSearchSheet from '$features/recipes/library/PromoteSearchSheet.svelte';
  import { useLibraryList } from '$features/recipes/library/useLibraryList.svelte';
  import {
    featuredQuery,
    useSuggestionLead
  } from '$features/recipes/library/useSuggestionLead.svelte';
  import LibraryToolbar from '$features/recipes/filters/LibraryToolbar.svelte';
  import RecipeGrid from '$features/recipes/RecipeGrid.svelte';
  import { libraryView } from '$features/recipes/stores/libraryView.svelte';
  import { recipes } from '$features/recipes/stores/recipes.svelte';
  import type { SavedSearch } from '$features/recipes/stores/savedSearches.svelte';
  import { suggestions } from '$features/recipes/stores/suggestions.svelte';
  import SuggestionDeck from '$features/recipes/SuggestionDeck.svelte';
  import { m } from '$shell/i18n';
  import Page from '$shell/Page.svelte';
  import PageAction from '$shell/PageAction.svelte';
  import PageHeader from '$shell/PageHeader.svelte';

  /** Everything the household can cook; the toolbar owns search and filters, this page owns what to do with the answer. */
  const householdId = $derived(session.activeHouseholdId);

  let shelving = $state<SavedSearch | null>(null);

  /** Empty because of a filter is a mistake to undo; empty because it is new is an invitation. */
  const filtered = $derived(libraryView.filtered);

  // `/?tag=…`, from a tag on a recipe: the library opens filtered by it, and the address is tidied so a reload keeps the toolbar's own state.
  $effect(() => {
    const tag = page.url.searchParams.get('tag');
    const household = householdId;

    if (tag && household) {
      untrack(() => {
        libraryView.forHousehold(household);
        libraryView.assign({ tags: [tag] });
        void goto(resolve('/(app)'), { replaceState: true });
      });
    }
  });

  const lead = useSuggestionLead({ householdId: () => householdId, filtered: () => filtered });
  const list = useLibraryList({ householdId: () => householdId, ranks: () => lead.ranks });

  /** Panel and grid are drawn together: whichever landed first would shift the page when the other arrives. */
  const arranged = $derived(
    (filtered || !householdId || suggestions.answered(householdId, featuredQuery)) &&
      !(recipes.status === 'loading' && recipes.items.length === 0)
  );

  const searchId = 'recipe-search';

  /** Below this a "suggestion" is just one of a handful of recipes, and the panel would show it twice: featured and in the grid. */
  const leadFrom = 6;
  const suggesting = $derived(arranged && lead.lead.length > 0 && recipes.total >= leadFrom);

  // Said once the shortlist is actually on screen, so tomorrow's list can lean away from today's.
  $effect(() => {
    if (householdId && suggesting) {
      void suggestions.reportShown(householdId, featuredQuery);
    }
  });
</script>

<svelte:head><title>{m['recipes.title']()}</title></svelte:head>

<svelte:window onkeydown={(event) => focusSearchOnFind(event, searchId)} />

{#snippet newRecipe()}
  <PageAction
    text={m['recipes.new.short']()}
    label={m['recipes.empty.action']()}
    href={resolve('/(app)/recipes/new')}
  />
{/snippet}

<Page>
  <!-- An empty library's own invitation is the one way to create there, so the header does not offer a second, equally loud one. -->
  <PageHeader
    title={m['recipes.title']()}
    subtitle={m['recipes.collection.subtitle']()}
    actions={recipes.status === 'ready' && recipes.total === 0 && !filtered ? undefined : newRecipe}
  />

  <!-- The diary's line lives in Woche's today, where planning and eating meet (culina-v2-6ilt.9), not above the recipes. -->
  {#if householdId}<TodayMeal {householdId} />{/if}

  <!-- An empty library has nothing to search or filter; it shows its invitation instead. -->
  {#if !(recipes.status === 'ready' && recipes.total === 0 && !filtered)}
    <LibraryToolbar
      id={searchId}
      keyShortcuts="Meta+F Control+F"
      householdId={householdId ?? ''}
      view={libraryView}
      context={list.context}
      searchLabel={m['recipes.list.searchLabel']()}
      searchPlaceholder={m['recipes.list.searchPlaceholder']()}
      savable
      primary
      onpromote={(search) => (shelving = search)}
      interpretation={recipes.status === 'ready' ? recipes.interpretation : null}
      total={recipes.total}
      onastyped={list.turnDownCorrection}
    >
      {#snippet summary()}
        <LibrarySummary {filtered} order={list.order} onreset={() => libraryView.clear()} />
      {/snippet}
    </LibraryToolbar>
  {/if}

  {#if recipes.status === 'failed' && recipes.items.length === 0}
    <ErrorState
      title={m['recipes.failed.title']()}
      body={m['recipes.failed.body']()}
      requestIdLabel={m['error.reference']()}
      requestId={recipes.error?.requestId}
    >
      {#snippet action()}
        <Button variant="primary" onclick={list.retry}>{m['error.retry']()}</Button>
      {/snippet}
    </ErrorState>
  {:else if recipes.status === 'ready' && recipes.items.length === 0 && filtered}
    <EmptyState title={m['recipes.filtered.title']()} body={m['recipes.filtered.body']()}>
      {#snippet action()}
        <Button onclick={() => libraryView.clear()}>{m['recipes.filtered.action']()}</Button>
      {/snippet}
    </EmptyState>
  {:else if recipes.status === 'ready' && recipes.items.length === 0}
    <EmptyLibrary />
  {:else}
    {#if recipes.status === 'failed'}
      <RefreshFailed onretry={list.retry} />
    {/if}

    {#if suggesting}
      <SuggestionDeck
        items={lead.lead}
        ondismiss={lead.shortlist.length > 0 ? (recipeId) => void lead.hide(recipeId) : undefined}
        inherited={session.inheritedFrom}
        onmore={lead.moreHandler()}
      />
    {/if}

    <RecipeGrid
      recipes={recipes.items}
      query={libraryView.query}
      loading={!arranged}
      onmore={list.autoLoads ? list.more : undefined}
      inherited={session.inheritedFrom}
    />

    {#if recipes.moreFailed}
      <div class="more">
        <p class="stalled">{m['recipes.list.moreFailed']()}</p>
        <Button onclick={list.more}>{m['error.retry']()}</Button>
      </div>
    {/if}
  {/if}
</Page>

<PromoteSearchSheet bind:search={shelving} {householdId} />

<style>
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
