<script lang="ts">
  import { resolve } from '$app/paths';
  import { page } from '$app/state';
  import { Button, ErrorState } from '$ds';
  import { session } from '$features/auth/session.svelte';
  import CookbookActions from '$features/cookbooks/CookbookActions.svelte';
  import CookbookHeading from '$features/cookbooks/CookbookHeading.svelte';
  import CookbookSheet from '$features/cookbooks/CookbookSheet.svelte';
  import DeleteCookbookDialog from '$features/cookbooks/DeleteCookbookDialog.svelte';
  import EmptyShelf from '$features/cookbooks/EmptyShelf.svelte';
  import { cookbooks } from '$features/cookbooks/stores/cookbooks.svelte';
  import { useCookbookActions } from '$features/cookbooks/useCookbookActions.svelte';
  import { useCookbookShelf } from '$features/cookbooks/useCookbookShelf.svelte';
  import LibraryToolbar from '$features/recipes/filters/LibraryToolbar.svelte';
  import RecipeGrid from '$features/recipes/RecipeGrid.svelte';
  import RecipePicker from '$features/recipes/RecipePicker.svelte';
  import { m } from '$shell/i18n';
  import NotFound from '$shell/NotFound.svelte';
  import Page from '$shell/Page.svelte';

  const cookbookId = $derived(page.params.cookbookId ?? '');
  const householdId = $derived(session.activeHouseholdId);

  const cookbook = $derived(cookbooks.open?.id === cookbookId ? cookbooks.open : null);

  const automatic = $derived(cookbook?.kind === 'smart');

  /** Every member, loaded when the picker opens; the shelf on screen is only its first page. */
  const taken = $derived(cookbooks.membersOf(cookbookId));

  const list = useCookbookShelf({ cookbookId: () => cookbookId, householdId: () => householdId });
  const { shelf, view } = list;

  const actions = useCookbookActions({
    cookbookId: () => cookbookId,
    cookbook: () => cookbook,
    householdId: () => householdId,
    reload: list.reload
  });

  $effect(() => {
    if (actions.ui.picking && cookbookId) {
      void cookbooks.loadMembers(cookbookId);
    }
  });
</script>

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
      <CookbookHeading {cookbook} loading={cookbooks.status === 'loading'} {automatic} />

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
          context={list.context}
          searchLabel={m['cookbooks.search']()}
          searchPlaceholder={m['cookbooks.search']()}
          interpretation={shelf.status === 'ready' ? shelf.interpretation : null}
          total={shelf.total}
          scope={cookbook?.name}
          onastyped={list.turnDownCorrection}
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
          <Button variant="primary" onclick={list.reload}>{m['error.retry']()}</Button>
        {/snippet}
      </ErrorState>
    {:else if shelf.status === 'ready' && shelf.items.length === 0}
      <EmptyShelf
        filtered={list.filtered}
        {automatic}
        onclear={() => view.clear()}
        onadd={() => (actions.ui.picking = true)}
        onedit={() => (actions.ui.renaming = true)}
      />
    {:else}
      <RecipeGrid
        recipes={shelf.items}
        query={view.query}
        loading={shelf.status === 'loading' && shelf.items.length === 0}
        onmore={list.autoLoads ? list.more : undefined}
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

  .tools {
    width: 100%;
  }

  .count {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }
</style>
