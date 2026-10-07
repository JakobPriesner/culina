<script lang="ts">
  import { Button, EmptyState, ErrorState, ProgressBar } from '$ds';
  import { units } from '$features/recipes/stores/units.svelte';
  import { session } from '$features/auth/session.svelte';
  import { nameOf, type Section } from '$features/shopping/sections';
  import ShoppingAddLine from '$features/shopping/ShoppingAddLine.svelte';
  import ShoppingFinishedNote from '$features/shopping/ShoppingFinishedNote.svelte';
  import ShoppingHeader from '$features/shopping/ShoppingHeader.svelte';
  import ShoppingListSkeleton from '$features/shopping/ShoppingListSkeleton.svelte';
  import ShoppingRecipePicker from '$features/shopping/ShoppingRecipePicker.svelte';
  import ShoppingSection from '$features/shopping/ShoppingSection.svelte';
  import { trackShopFinished } from '$features/shopping/shopFinished.svelte';
  import { shopping, type ShoppingItem } from '$features/shopping/stores/shopping.svelte';
  import { explain } from '$shell/explain';
  import { toaster } from '$shell/toaster.svelte';
  import { m } from '$shell/i18n';
  import Olli from '$shell/olli/Olli.svelte';
  import Page from '$shell/Page.svelte';

  /** The shopping list as used in a shop: sections in walking order, bought items at the bottom, one bulk clear. */
  const householdId = $derived(session.activeHouseholdId);

  let picking = $state(false);

  /** Lines still to find; the subtitle only says it while it is a number worth having. */
  const remaining = $derived(shopping.items.length - shopping.bought.length);

  const shop = trackShopFinished();

  /** A progress bar needs at least one line: over an empty list it would show nought per cent. */
  const started = $derived(shopping.status === 'ready' && shopping.items.length > 0);

  $effect(() => {
    if (householdId) {
      void shopping.load(householdId);
      void units.load(householdId);
    }
  });

  /** Moves a line to its shop section, announced once done since the line leaves for another heading. */
  async function move(item: ShoppingItem, section: Section) {
    if (!householdId) {
      return;
    }

    const failure = await shopping.moveToSection(householdId, item.itemId, section);

    toaster.show(
      failure
        ? { message: () => explain(failure), tone: 'danger' }
        : { message: () => m['shopping.moved']({ name: item.name, section: nameOf(section) }) }
    );
  }

  const check = (item: ShoppingItem, checked: boolean) =>
    householdId && shopping.check(householdId, item.itemId, checked);

  const remove = (item: ShoppingItem) => householdId && shopping.remove(householdId, item.itemId);

  function retry() {
    shopping.clearError();

    if (householdId) {
      void shopping.load(householdId);
    }
  }
</script>

{#snippet peeking()}<Olli pose="peeking" />{/snippet}
<svelte:head><title>{m['shopping.title']()}</title></svelte:head>

<Page width="reading">
  <ShoppingHeader {remaining} onaddrecipe={() => (picking = true)} />

  <!-- A glanceable bar for how much of the shop is done; the sentence above gives what is left. -->
  {#if started}
    <div class="progress">
      <ProgressBar
        value={shopping.bought.length}
        max={shopping.items.length}
        label={m['shopping.title']()}
        valueText={m['shopping.progress']({
          done: shopping.bought.length,
          total: shopping.items.length
        })}
      />
    </div>
  {/if}

  <ShoppingAddLine {householdId} />

  {#if shopping.status === 'failed'}
    <ErrorState
      title={m['recipes.failed.title']()}
      body={m['recipes.failed.body']()}
      requestIdLabel={m['error.reference']()}
      requestId={shopping.error?.requestId}
    >
      {#snippet action()}
        <Button variant="primary" onclick={retry}>{m['error.retry']()}</Button>
      {/snippet}
    </ErrorState>
  {:else if shopping.status === 'loading' && shopping.items.length === 0}
    <ShoppingListSkeleton />
  {:else if shopping.status === 'ready' && shopping.items.length === 0}
    <EmptyState title={m['shopping.empty.title']()} body={m['shopping.empty.body']()} art={peeking}>
      {#snippet action()}
        <Button variant="primary" onclick={() => (picking = true)}>
          {m['shopping.addRecipe']()}
        </Button>
      {/snippet}
    </EmptyState>
  {:else}
    {#each shopping.toBuy as group (group.section)}
      <ShoppingSection
        label={nameOf(group.section)}
        count={group.items.length}
        items={group.items}
        oncheck={check}
        onremove={remove}
        onmove={(item, section) => void move(item, section)}
      />
    {/each}

    {#if shop.finished}
      <ShoppingFinishedNote celebrate={shop.justFinished} />
    {/if}

    {#if shopping.bought.length > 0}
      <ShoppingSection
        apart
        label={m['shopping.bought']({ count: shopping.bought.length })}
        items={shopping.bought}
        oncheck={check}
        onremove={remove}
        onmove={(item, section) => void move(item, section)}
      >
        {#snippet action()}
          <!-- Next to what it clears, not at the page top. -->
          <Button size="sm" onclick={() => householdId && shopping.clearBought(householdId)}>
            {m['shopping.clearBought']()}
          </Button>
        {/snippet}
      </ShoppingSection>
    {/if}
  {/if}
</Page>

{#if householdId}
  <ShoppingRecipePicker bind:open={picking} {householdId} />
{/if}

<style>
  .progress {
    margin-block: var(--space-6) var(--space-4);
  }
</style>
