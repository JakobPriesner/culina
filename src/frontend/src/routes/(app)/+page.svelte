<script lang="ts">
  import { resolve } from '$app/paths';
  import { Button, EmptyState, ErrorState, SearchField } from '$ds';
  import RecipeGrid from '$features/recipes/RecipeGrid.svelte';
  import { recipes } from '$features/recipes/stores/recipes.svelte';
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
  let search = $state('');
  let applied = $state('');

  let debounce: ReturnType<typeof setTimeout> | undefined;

  const householdId = $derived(session.activeHouseholdId);

  /** Empty because of a filter is a mistake to undo; empty because it is new is an invitation. */
  const filtered = $derived(applied.trim().length > 0);

  $effect(() => {
    if (householdId) {
      void recipes.list(householdId, { query: applied });
    }
  });

  $effect(() => () => clearTimeout(debounce));

  function type(value: string) {
    search = value;
    clearTimeout(debounce);

    // Long enough that a word is finished, short enough that it feels live.
    debounce = setTimeout(() => (applied = value), 250);
  }

  function clear() {
    search = '';
    clearTimeout(debounce);
    applied = '';
  }

  function retry() {
    recipes.clearError();

    if (householdId) {
      void recipes.list(householdId, { query: applied });
    }
  }
</script>

<svelte:head><title>{m['recipes.title']()}</title></svelte:head>

<Page>
  <header class="head">
    <div class="heading">
      <h1 class="title">{m['recipes.title']()}</h1>
      {#if recipes.status === 'ready'}
        <p class="count">{m['recipes.list.count']({ count: recipes.total })}</p>
      {/if}
    </div>

    <div class="actions">
      <Button variant="primary" href={resolve('/(app)/recipes/new')}>
        {m['recipes.empty.action']()}
      </Button>
    </div>

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
  </header>

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
        <Button onclick={clear}>{m['recipes.filtered.action']()}</Button>
      {/snippet}
    </EmptyState>
  {:else if recipes.status === 'ready' && recipes.items.length === 0}
    <EmptyState title={m['recipes.empty.title']()} body={m['recipes.empty.body']()}>
      {#snippet action()}
        <Button variant="primary" href={resolve('/(app)/recipes/new')}>
          {m['recipes.empty.action']()}
        </Button>
      {/snippet}
    </EmptyState>
  {:else}
    <RecipeGrid
      recipes={recipes.items}
      loading={recipes.status === 'loading' && recipes.items.length === 0}
    />

    {#if recipes.hasMore}
      <div class="more">
        <Button onclick={() => householdId && recipes.loadMore(householdId, { query: applied })}>
          {m['recipes.list.more']()}
        </Button>
      </div>
    {/if}
  {/if}
</Page>

<style>
  .head {
    display: flex;
    flex-wrap: wrap;
    align-items: flex-end;
    justify-content: space-between;
    gap: var(--space-4);
    margin-bottom: var(--space-8);
  }

  .heading {
    display: flex;
    align-items: baseline;
    gap: var(--space-3);
  }

  .title {
    font-family: var(--font-editorial);
    font-size: var(--text-display);
    font-weight: var(--weight-regular);
    letter-spacing: -0.03em;
  }

  .count {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .search {
    width: min(22rem, 100%);
  }

  .actions {
    order: 1;
  }

  .more {
    display: flex;
    justify-content: center;
    margin-top: var(--space-8);
  }
</style>
