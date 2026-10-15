<script lang="ts">
  import { Button, EmptyState, ErrorState, ProgressBar } from '$ds';
  import { units } from '$features/recipes/stores/units.svelte';
  import { session } from '$features/auth/session.svelte';
  import { nameOf, type Section } from '$features/shopping/sections';
  import ShoppingAddLine from '$features/shopping/ShoppingAddLine.svelte';
  import ShoppingFinishedNote from '$features/shopping/ShoppingFinishedNote.svelte';
  import ShoppingHeader from '$features/shopping/ShoppingHeader.svelte';
  import ShoppingMeals from '$features/shopping/ShoppingMeals.svelte';
  import { plannedMealsOf } from '$features/shopping/plannedMeals';
  import ShoppingListSkeleton from '$features/shopping/ShoppingListSkeleton.svelte';
  import ShoppingRecipePicker from '$features/shopping/ShoppingRecipePicker.svelte';
  import ShoppingSection from '$features/shopping/ShoppingSection.svelte';
  import { groupsToBuy } from '$features/shopping/lineRuns';
  import { createLingering } from '$features/shopping/lingering.svelte';
  import { trackShopFinished } from '$features/shopping/shopFinished.svelte';
  import { shopping, type ShoppingItem } from '$features/shopping/stores/shopping.svelte';
  import { asDate } from '$features/planning/mealPlan.svelte';
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

  /** Which planned meals the lines still to buy come from, so an old one is seen for what it is rather than silently added up. */
  const plannedMeals = $derived(plannedMealsOf(shopping.items, asDate(new Date())));

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

  const lingering = createLingering();

  const toBuy = $derived(groupsToBuy(shopping.items, lingering.ids));
  const bought = $derived(shopping.bought.filter((item) => !lingering.ids.includes(item.itemId)));

  /** The last tick's Undo; a new tick replaces it, so ticking through a shop does not stack messages. */
  let tickToast: string | null = null;

  function check(item: ShoppingItem, checked: boolean) {
    const household = householdId;

    if (!household) {
      return;
    }

    if (checked) {
      lingering.hold(item.itemId);

      if (tickToast) toaster.dismiss(tickToast);
      tickToast = toaster.show({
        message: () => m['shopping.ticked']({ name: item.name }),
        action: {
          label: m['shopping.ticked.undo'],
          run: () => void shopping.check(household, item.itemId, false)
        },
        durationMs: 4000
      });
    }

    void shopping.check(household, item.itemId, checked);
  }

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
  <ShoppingHeader {remaining} />

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

  <ShoppingMeals days={plannedMeals} />

  <ShoppingAddLine {householdId} onaddrecipe={() => (picking = true)} />

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
    {#each toBuy as group (group.section)}
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

    {#if bought.length > 0}
      <ShoppingSection
        apart
        label={m['shopping.completed']()}
        count={bought.length}
        items={bought}
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
    margin-block: var(--space-2) var(--space-6);
  }
</style>
