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

  /**
   * The list, as it is actually used: standing in a shop, one hand free.
   *
   * Sections in the order a shop is walked, what is already in the trolley kept
   * visible at the bottom, and one bulk action — clearing what is bought —
   * because after a shop removing a dozen lines one at a time is the tedium
   * this exists to avoid.
   */
  const householdId = $derived(session.activeHouseholdId);

  /** Whether the recipe picker is up. */
  let picking = $state(false);

  /**
   * How many lines are still to find, which is the number a shopper wants.
   *
   * Said in the subtitle only while it is a number worth having. "0 still to
   * buy" is a count of nothing, and the finished list says so in words
   * further down, where the list itself would have been.
   */
  const remaining = $derived(shopping.items.length - shopping.bought.length);

  const shop = trackShopFinished();

  /**
   * Whether the page is far enough along to say how far along it is.
   *
   * A bar over an empty list is a bar at nought per cent, which is a graphic
   * of nothing. It appears with the first line and goes with the last.
   */
  const started = $derived(shopping.status === 'ready' && shopping.items.length > 0);

  $effect(() => {
    if (householdId) {
      void shopping.load(householdId);
      void units.load(householdId);
    }
  });

  /**
   * A line into the part of the shop it is actually in.
   *
   * Said out loud once it has gone through, because the line has just left
   * the screen for another heading — and because the list will put the same
   * name there next time, which nothing else on the page would tell anybody.
   */
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

  <!--
    How much of the shop is done, as a shape rather than as a sentence.

    The sentence above is the number a shopper wants — what is left — and it is
    the one worth reading. This is the one worth glancing at: halfway down a
    long aisle, "am I nearly finished" is answered by a bar in the corner of the
    eye without anybody having to count what is struck through at the bottom of
    the page.
  -->
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
    <!-- The shape of a list rather than a spinner, so the page does not sit
         blank and then jump. -->
    <ShoppingListSkeleton />
  {:else if shopping.status === 'ready' && shopping.items.length === 0}
    <EmptyState title={m['shopping.empty.title']()} body={m['shopping.empty.body']()} art={peeking}>
      {#snippet action()}
        <!-- The invitation is the thing that fills a list fastest, and it is
             the same one the button above offers. Sending somebody off to the
             recipe list to find it themselves was a longer way round. -->
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
          <!-- Next to what it clears. At the top of the page it was an action
               with nothing near it to explain what it would take away. -->
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
