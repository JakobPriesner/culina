<script lang="ts">
  import { Button, EmptyState, ErrorState } from '$ds';
  import CookbookGrid from '$features/cookbooks/CookbookGrid.svelte';
  import CookbookSheet from '$features/cookbooks/CookbookSheet.svelte';
  import { cookbooks } from '$features/cookbooks/stores/cookbooks.svelte';
  import type { CookbookRules } from '$features/cookbooks/types';
  import { session } from '$features/auth/session.svelte';
  import { m } from '$shell/i18n';
  import Olli from '$shell/olli/Olli.svelte';
  import LibraryActions from '$shell/LibraryActions.svelte';
  import PageHeader from '$shell/PageHeader.svelte';
  import Page from '$shell/Page.svelte';
  import { toaster } from '$shell/toaster.svelte';

  /** Every shelf the household has; no search box, since a handful of cookbooks are all on screen and searching happens inside one. */
  const householdId = $derived(session.activeHouseholdId);

  let making = $state(false);
  let saving = $state(false);

  $effect(() => {
    if (householdId) {
      void cookbooks.list(householdId);
    }
  });

  const autoLoads = $derived(cookbooks.hasMore && !cookbooks.moreFailed);

  function retry() {
    cookbooks.clearError();

    if (householdId) {
      void cookbooks.list(householdId);
    }
  }

  function more() {
    if (householdId) {
      void cookbooks.loadMore(householdId);
    }
  }

  async function make(name: string, description: string | null, rules: CookbookRules | null) {
    if (!householdId) {
      return;
    }

    saving = true;

    const created = await cookbooks.create(householdId, name, description ?? undefined, rules);

    saving = false;

    if (created) {
      making = false;

      return;
    }

    toaster.show({ message: () => m['cookbooks.add.failed'](), tone: 'danger' });
  }
</script>

{#snippet peeking()}<Olli pose="peeking" />{/snippet}
<svelte:head><title>{m['cookbooks.title']()}</title></svelte:head>

<Page>
  <PageHeader title={m['cookbooks.title']()} subtitle={m['cookbooks.subtitle']()}>
    {#snippet actions()}
      <LibraryActions oncreatecookbook={() => (making = true)} />
    {/snippet}
  </PageHeader>

  {#if cookbooks.status === 'failed'}
    <ErrorState
      title={m['cookbooks.failed.title']()}
      body={m['cookbooks.failed.body']()}
      requestIdLabel={m['error.reference']()}
      requestId={cookbooks.error?.requestId}
    >
      {#snippet action()}
        <Button variant="primary" onclick={retry}>{m['error.retry']()}</Button>
      {/snippet}
    </ErrorState>
  {:else if cookbooks.status === 'ready' && cookbooks.items.length === 0}
    <!-- No filter means no filtered-empty case: one empty state, an invitation. -->
    <EmptyState
      title={m['cookbooks.empty.title']()}
      body={m['cookbooks.empty.body']()}
      art={peeking}
    >
      {#snippet action()}
        <Button variant="primary" onclick={() => (making = true)}>
          {m['cookbooks.empty.action']()}
        </Button>
      {/snippet}
    </EmptyState>
  {:else}
    <CookbookGrid
      cookbooks={cookbooks.items}
      loading={cookbooks.status === 'loading' && cookbooks.items.length === 0}
      onmore={autoLoads ? more : undefined}
    />
  {/if}
</Page>

<CookbookSheet
  open={making}
  householdId={householdId ?? ''}
  {saving}
  onsave={make}
  onclose={() => (making = false)}
/>
