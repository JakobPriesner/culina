<script lang="ts">
  import { goto } from '$app/navigation';
  import { resolve } from '$app/paths';
  import { page } from '$app/state';
  import { Button, EmptyState, ErrorState } from '$ds';
  import { session } from '$features/auth/session.svelte';
  import { cookbooks } from '$features/cookbooks/stores/cookbooks.svelte';
  import CookbookSheet from '$features/cookbooks/CookbookSheet.svelte';
  import type { CookbookRules } from '$features/cookbooks/types';
  import RecipeGrid from '$features/recipes/RecipeGrid.svelte';
  import RecipePicker from '$features/recipes/RecipePicker.svelte';
  import LibraryToolbar from '$features/recipes/filters/LibraryToolbar.svelte';
  import { createRecipeStore } from '$features/recipes/stores/recipes.svelte';
  import { effectiveSort, RecipeQuery } from '$features/recipes/stores/libraryView.svelte';
  import { shopping } from '$features/shopping/stores/shopping.svelte';
  import { explain } from '$shell/explain';
  import { m } from '$shell/i18n';
  import Page from '$shell/Page.svelte';
  import { toaster } from '$shell/toaster.svelte';

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

  let renaming = $state(false);
  let saving = $state(false);
  let picking = $state(false);
  let shopping_ = $state(false);

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
    sort: order
  });

  const autoLoads = $derived(shelf.hasMore && !shelf.moreFailed);

  /** What is already on the shelf, so the picker can say so rather than repeat it. */
  const taken = $derived(shelf.items.map((recipe) => recipe.id));

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

  async function rename(name: string, description: string | null, rules: CookbookRules | null) {
    saving = true;

    const done = await cookbooks.rename(cookbookId, name, description, rules);

    saving = false;

    if (done) {
      renaming = false;
      // The rules decide what is on it, so changing them changes the shelf.
      reload();

      return;
    }

    toaster.show({
      message: cookbooks.error ? explain(cookbooks.error) : m['cookbooks.add.failed'](),
      tone: 'danger'
    });
  }

  async function remove() {
    const name = cookbook?.name ?? '';
    const done = await cookbooks.remove(cookbookId);

    if (!done) {
      toaster.show({ message: m['cookbooks.delete.failed'](), tone: 'danger' });

      return;
    }

    // Said plainly, because "delete" next to a list of recipes is a frightening
    // word and the reassurance is the true part.
    toaster.show({ message: m['cookbooks.delete.done']({ name }) });

    await goto(resolve('/(app)/cookbooks'));
  }

  async function add(recipeId: string, title: string) {
    if (!cookbook) {
      return;
    }

    const done = await cookbooks.setOn(recipeId, { id: cookbook.id, name: cookbook.name }, true);

    if (!done) {
      toaster.show({ message: m['cookbooks.add.failed'](), tone: 'danger' });

      return;
    }

    toaster.show({ message: m['cookbooks.addRecipes.added']({ title }) });
    reload();
  }

  /**
   * The whole shelf, onto the shopping list.
   *
   * One call per recipe, through the path a single recipe already takes. A
   * second endpoint that merged a shelf at once would be a second place for
   * merging to behave differently, and merging is the entire value of the list.
   * It is also why a partial failure is reported rather than rolled back: some
   * of it is genuinely on the list, and somebody may be reading it in a shop.
   */
  async function addToShoppingList() {
    if (!householdId || shelf.items.length === 0) {
      toaster.show({ message: m['cookbooks.shopping.empty']() });

      return;
    }

    shopping_ = true;

    const wanted = [...shelf.items];
    let done = 0;

    for (const recipe of wanted) {
      const failure = await shopping.addRecipe(householdId, recipe.id, recipe.yieldAmount);

      if (!failure) {
        done += 1;
      }
    }

    shopping_ = false;

    toaster.show(
      done === wanted.length
        ? { message: m['cookbooks.shopping.done']({ count: done }) }
        : {
            message: m['cookbooks.shopping.partial']({ done, total: wanted.length }),
            tone: 'danger'
          }
    );
  }
</script>

<svelte:head><title>{cookbook?.name ?? m['cookbooks.title']()}</title></svelte:head>

<Page>
  {#if cookbooks.status === 'failed' && !cookbook}
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
        <h1 class="title">{cookbook?.name ?? ''}</h1>

        {#if cookbook?.description}
          <p class="subtitle">{cookbook.description}</p>
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

      <div class="actions">
        <!-- The one accented thing on the screen. On a shelf you fill, that is
             putting something on it; on one that fills itself, there is nothing
             to put, so the rules are the thing you came to change. -->
        {#if automatic}
          <Button variant="primary" onclick={() => (renaming = true)}>
            {m['cookbooks.rules.edit']()}
          </Button>
        {:else}
          <Button variant="primary" onclick={() => (picking = true)}>
            {m['cookbooks.addRecipes.action']()}
          </Button>
        {/if}

        <Button loading={shopping_} onclick={() => void addToShoppingList()}>
          {m['cookbooks.shopping.add']()}
        </Button>

        {#if !automatic}
          <Button variant="ghost" onclick={() => (renaming = true)}>
            {m['cookbooks.edit.action']()}
          </Button>
        {/if}

        <!-- Quiet, and last. Loudness is not the same as safety: a filled red
             button is the first thing the eye lands on, which is exactly wrong
             for the one action nobody comes here to perform. There is no
             confirmation because there is nothing much to lose — the recipes
             all survive, and what goes is a name and a description — and the
             toast says so in those words. -->
        <Button variant="ghost" onclick={() => void remove()}>
          {m['cookbooks.delete.action']()}
        </Button>
      </div>

      <div class="tools">
        <LibraryToolbar
          id="cookbook-search"
          householdId={householdId ?? ''}
          {view}
          {context}
          searchLabel={m['cookbooks.search']()}
          searchPlaceholder={m['cookbooks.search']()}
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
      >
        {#snippet action()}
          {#if automatic}
            <Button variant="primary" onclick={() => (renaming = true)}>
              {m['cookbooks.rules.edit']()}
            </Button>
          {:else}
            <Button variant="primary" onclick={() => (picking = true)}>
              {m['cookbooks.addRecipes.action']()}
            </Button>
          {/if}
        {/snippet}
      </EmptyState>
    {:else}
      <RecipeGrid
        recipes={shelf.items}
        loading={shelf.status === 'loading' && shelf.items.length === 0}
        onmore={autoLoads ? more : undefined}
      />
    {/if}
  {/if}
</Page>

<CookbookSheet
  open={renaming}
  householdId={householdId ?? ''}
  {cookbook}
  {saving}
  onsave={rename}
  onclose={() => (renaming = false)}
/>

{#if householdId && !automatic}
  <RecipePicker
    open={picking}
    {householdId}
    title={m['cookbooks.addRecipes.title']()}
    {taken}
    onpick={(recipe) => void add(recipe.id, recipe.title)}
    onclose={() => (picking = false)}
  >
    {#snippet footer()}
      <Button variant="primary" onclick={() => (picking = false)}>
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

  .actions {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-3);
  }

  .tools {
    width: 100%;
  }

  .count {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }
</style>
