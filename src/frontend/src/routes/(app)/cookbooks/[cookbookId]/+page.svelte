<script lang="ts">
  import { resolve } from '$app/paths';
  import { page } from '$app/state';
  import { Button, EmptyState, ErrorState, Skeleton } from '$ds';
  import { session } from '$features/auth/session.svelte';
  import CookbookActions from '$features/cookbooks/CookbookActions.svelte';
  import DeleteCookbookDialog from '$features/cookbooks/DeleteCookbookDialog.svelte';
  import { cookbooks } from '$features/cookbooks/stores/cookbooks.svelte';
  import CookbookSheet from '$features/cookbooks/CookbookSheet.svelte';
  import { useCookbookActions } from '$features/cookbooks/useCookbookActions.svelte';
  import RecipeGrid from '$features/recipes/RecipeGrid.svelte';
  import RecipePicker from '$features/recipes/RecipePicker.svelte';
  import LibraryToolbar from '$features/recipes/filters/LibraryToolbar.svelte';
  import { createRecipeStore } from '$features/recipes/stores/recipes.svelte';
  import { effectiveSort, RecipeQuery } from '$features/recipes/stores/libraryView.svelte';
  import { m } from '$shell/i18n';
  import Olli from '$shell/olli/Olli.svelte';
  import NotFound from '$shell/NotFound.svelte';
  import Page from '$shell/Page.svelte';

  /**
   * One cookbook.
   *
   * Almost nothing of this page is its own. The shelf's recipes are the recipe
   * store with a `cookbookId` filter, drawn by the same grid the collection
   * uses, so searching, the skeletons, the infinite scroll and the empty states
   * arrived here already written. What is genuinely new is the header and the
   * two things you can do to a shelf: put something on it, and take the whole
   * of it to the shop.
   */
  const cookbookId = $derived(page.params.cookbookId ?? '');
  const householdId = $derived(session.activeHouseholdId);

  /**
   * This page's own list.
   *
   * Not the app's shared one: the collection behind this page is looking at
   * everything, and filtering that store to one shelf would leave it filtered
   * when somebody navigates back.
   */
  const shelf = createRecipeStore();

  /**
   * What this shelf is being asked for.
   *
   * Its own, not the library's: the collection behind this page is looking at
   * everything, and sharing one question would leave the library filtered to a
   * shelf when somebody navigates back. The same class either way, so a shelf
   * can be sorted and narrowed exactly as the library can.
   */
  const view = new RecipeQuery();

  const cookbook = $derived(cookbooks.open?.id === cookbookId ? cookbooks.open : null);

  /** A shelf that fills itself has nothing to put on it by hand. */
  const automatic = $derived(cookbook?.kind === 'smart');

  /** Empty because of a filter is a mistake to undo; empty because it is new is an invitation. */
  const filtered = $derived(view.filtered);

  /**
   * A shelf is read in the order it was built, until somebody says otherwise.
   *
   * `ranks` is false here rather than plumbed through: "for tonight" ranks the
   * whole library, and offering it inside a shelf would promise an order over
   * the shelf that it does not mean.
   */
  const context = $derived({
    searching: view.query.trim().length > 0,
    ranks: false,
    inACookbook: true
  });

  const order = $derived(effectiveSort(view.sort, context));

  /** The query whose correction the reader turned down. */
  let asTypedFor = $state<string | null>(null);

  /**
   * Which list is on screen.
   *
   * Built once, so the first page, the next page and the retry cannot ask for
   * three different things.
   */
  const filters = $derived({
    query: view.query,
    tags: view.tags,
    maxMinutes: view.maxMinutes ?? undefined,
    cookbookId,
    sort: order,
    asTyped: asTypedFor !== null && asTypedFor === view.query
  });

  const autoLoads = $derived(shelf.hasMore && !shelf.moreFailed);

  /**
   * Everything already on the shelf, so the picker can mark it before it is
   * touched. Asked when the picker opens rather than read off the shelf on
   * screen, which is only its first page.
   */
  const taken = $derived(cookbooks.membersOf(cookbookId));

  const actions = useCookbookActions({
    cookbookId: () => cookbookId,
    cookbook: () => cookbook,
    householdId: () => householdId,
    reload
  });

  $effect(() => {
    if (actions.ui.picking && cookbookId) {
      void cookbooks.loadMembers(cookbookId);
    }
  });

  $effect(() => {
    if (cookbookId) {
      void cookbooks.load(cookbookId);
    }
  });

  $effect(() => {
    if (householdId && cookbookId) {
      void shelf.list(householdId, filters);
    }
  });

  function clear() {
    view.clear();
  }

  function more() {
    if (householdId && cookbookId) {
      void shelf.loadMore(householdId, filters);
    }
  }

  function reload() {
    if (householdId && cookbookId) {
      void shelf.list(householdId, filters);
      void cookbooks.load(cookbookId);
    }
  }
</script>

{#snippet peeking()}<Olli pose="peeking" />{/snippet}
<svelte:head><title>{cookbook?.name ?? m['cookbooks.title']()}</title></svelte:head>

<Page>
  {#if cookbooks.status === 'failed' && !cookbook && cookbooks.error?.status === 404}
    <NotFound kind="cookbook" level={1} />
  {:else if cookbooks.status === 'failed' && !cookbook}
    <ErrorState
      title={m['cookbooks.detail.failed']()}
      body={m['cookbooks.detail.failed.body']()}
      requestIdLabel={m['error.reference']()}
      requestId={cookbooks.error?.requestId}
    >
      {#snippet action()}
        <Button variant="primary" href={resolve('/(app)/cookbooks')}>
          {m['cookbooks.title']()}
        </Button>
      {/snippet}
    </ErrorState>
  {:else}
    <header class="head">
      <div class="heading">
        <p class="eyebrow">{m['cookbooks.title']()}</p>
        {#if !cookbook && cookbooks.status === 'loading'}
          <Skeleton width="16rem" height="2.25rem" />
          <Skeleton width="24rem" height="1.25rem" />
        {:else}
          <h1 class="title">{cookbook?.name ?? ''}</h1>

          {#if cookbook?.description}
            <p class="subtitle">{cookbook.description}</p>
          {/if}
        {/if}

        <!-- What it asks for, in the words somebody chose, so the shelf
             explains itself rather than being a list you have to trust. -->
        {#if automatic && cookbook?.rules}
          <p class="rules">
            <span class="automatic">{m['cookbooks.kind.label']()}</span>
            {[
              ...cookbook.rules.tags,
              ...cookbook.rules.ingredients,
              ...(cookbook.rules.maxMinutes === null
                ? []
                : [m['cookbooks.rules.minutes']({ count: cookbook.rules.maxMinutes })])
            ].join(' · ')}
          </p>
        {/if}
      </div>

      <CookbookActions
        {automatic}
        addingToList={actions.ui.addingToList}
        onedit={() => (actions.ui.renaming = true)}
        onpick={() => (actions.ui.picking = true)}
        onaddtolist={() => void actions.addToShoppingList()}
        ondelete={actions.askToDelete}
      />

      <div class="tools">
        <LibraryToolbar
          id="cookbook-search"
          householdId={householdId ?? ''}
          {view}
          {context}
          searchLabel={m['cookbooks.search']()}
          searchPlaceholder={m['cookbooks.search']()}
          interpretation={shelf.status === 'ready' ? shelf.interpretation : null}
          total={shelf.total}
          scope={cookbook?.name}
          onastyped={() => (asTypedFor = view.query)}
        >
          {#snippet summary()}
            {#if shelf.status === 'ready'}
              <p class="count">{m['cookbooks.card.count']({ count: shelf.total })}</p>
            {/if}
          {/snippet}
        </LibraryToolbar>
      </div>
    </header>

    {#if shelf.status === 'failed'}
      <ErrorState
        title={m['recipes.failed.title']()}
        body={m['recipes.failed.body']()}
        requestIdLabel={m['error.reference']()}
        requestId={shelf.error?.requestId}
      >
        {#snippet action()}
          <Button variant="primary" onclick={reload}>{m['error.retry']()}</Button>
        {/snippet}
      </ErrorState>
    {:else if shelf.status === 'ready' && shelf.items.length === 0 && filtered}
      <EmptyState
        title={m['cookbooks.detail.filtered.title']()}
        body={m['cookbooks.detail.filtered.body']()}
      >
        {#snippet action()}
          <Button onclick={clear}>{m['recipes.filtered.action']()}</Button>
        {/snippet}
      </EmptyState>
    {:else if shelf.status === 'ready' && shelf.items.length === 0}
      <!-- Empty because nothing matches is a rule to loosen; empty because it
           is new is an invitation to add something. Different mistakes, so
           different offers. -->
      <EmptyState
        title={m['cookbooks.detail.empty.title']()}
        body={automatic ? m['cookbooks.detail.noMatch.body']() : m['cookbooks.detail.empty.body']()}
        art={!automatic ? peeking : undefined}
      >
        {#snippet action()}
          {#if automatic}
            <Button variant="primary" onclick={() => (actions.ui.renaming = true)}>
              {m['cookbooks.rules.edit']()}
            </Button>
          {:else}
            <Button variant="primary" onclick={() => (actions.ui.picking = true)}>
              {m['cookbooks.addRecipes.action']()}
            </Button>
          {/if}
        {/snippet}
      </EmptyState>
    {:else}
      <RecipeGrid
        recipes={shelf.items}
        query={view.query}
        loading={shelf.status === 'loading' && shelf.items.length === 0}
        onmore={autoLoads ? more : undefined}
        inherited={session.inheritedFrom}
      />
    {/if}
  {/if}
</Page>

<DeleteCookbookDialog
  open={actions.ui.confirmingDelete}
  name={cookbook?.name ?? ''}
  deleting={actions.ui.deleting}
  error={actions.ui.deleteFailure}
  onconfirm={() => void actions.remove()}
  onclose={() => (actions.ui.confirmingDelete = false)}
/>

<CookbookSheet
  open={actions.ui.renaming}
  householdId={householdId ?? ''}
  {cookbook}
  saving={actions.ui.saving}
  onsave={actions.rename}
  onclose={() => (actions.ui.renaming = false)}
/>

{#if householdId && !automatic}
  <RecipePicker
    open={actions.ui.picking}
    {householdId}
    title={m['cookbooks.addRecipes.title']()}
    {taken}
    onpick={(recipe) => void actions.add(recipe.id, recipe.title)}
    onremove={(recipe) => void actions.takeOff(recipe.id, recipe.title)}
    onclose={() => (actions.ui.picking = false)}
  >
    {#snippet footer()}
      <Button variant="primary" onclick={() => (actions.ui.picking = false)}>
        {m['cookbooks.addRecipes.done']()}
      </Button>
    {/snippet}
  </RecipePicker>
{/if}

<style>
  .head {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-4);
    margin-bottom: var(--space-8);
    row-gap: var(--space-8);
  }

  .heading {
    flex: 1 1 24rem;
    min-width: 0;
  }

  .eyebrow {
    color: var(--text-muted);
    font-size: var(--text-xs);
    letter-spacing: 0.08em;
    text-transform: uppercase;
  }

  .title {
    font-family: var(--font-editorial);
    font-size: var(--text-display);
    font-weight: var(--weight-regular);
    letter-spacing: -0.03em;
  }

  .subtitle {
    color: var(--text-muted);
    margin-top: var(--space-1);
    max-width: 42ch;
  }

  .rules {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-2);
    margin-top: var(--space-3);
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .automatic {
    padding: var(--space-1) var(--space-2);
    border-radius: var(--radius-sm);
    background: var(--surface-accent-subtle);
    color: var(--accent);
    font-size: var(--text-xs);
    letter-spacing: 0.06em;
    text-transform: uppercase;
  }

  .tools {
    width: 100%;
  }

  .count {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }
</style>
