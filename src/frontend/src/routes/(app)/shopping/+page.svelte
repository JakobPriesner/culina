<script lang="ts">
  import { Button, EmptyState, ErrorState } from '$ds';
  import { parseIngredientLine } from '$features/recipes/editor/parseIngredientLine';
  import { session } from '$features/auth/session.svelte';
  import { nameOf } from '$features/shopping/sections';
  import ShoppingItemRow from '$features/shopping/ShoppingItemRow.svelte';
  import { shopping } from '$features/shopping/stores/shopping.svelte';
  import { m } from '$shell/i18n';
  import Page from '$shell/Page.svelte';

  /**
   * The list, as it is actually used: standing in a shop, one hand free.
   *
   * Sections in the order a shop is walked, what is already in the trolley kept
   * visible at the bottom, and one bulk action — clearing what is bought —
   * because after a shop removing a dozen lines one at a time is the tedium
   * this exists to avoid.
   */
  let typed = $state('');

  const householdId = $derived(session.activeHouseholdId);

  $effect(() => {
    if (householdId) {
      void shopping.load(householdId);
    }
  });

  /** One line in, the same way an ingredient is written into a recipe. */
  async function add(event: SubmitEvent) {
    event.preventDefault();

    const text = typed.trim();

    if (!text || !householdId) {
      return;
    }

    const parsed = parseIngredientLine(text);

    typed = '';

    await shopping.add(
      householdId,
      parsed.name,
      parsed.quantity.value ?? undefined,
      parsed.quantity.unit
    );
  }
</script>

<svelte:head><title>{m['shopping.title']()}</title></svelte:head>

<Page>
  <header class="head">
    <h1 class="title">{m['shopping.title']()}</h1>

    {#if shopping.bought.length > 0}
      <Button onclick={() => householdId && shopping.clearBought(householdId)}>
        {m['shopping.clearBought']()}
      </Button>
    {/if}
  </header>

  <form class="add" onsubmit={add}>
    <input
      class="ds-control"
      id="shopping-add"
      type="text"
      bind:value={typed}
      aria-label={m['shopping.add']()}
      placeholder={m['shopping.addPlaceholder']()}
    />
    <Button type="submit" variant="primary">{m['shopping.add']()}</Button>
  </form>

  {#if shopping.status === 'failed'}
    <ErrorState
      title={m['recipes.failed.title']()}
      body={m['recipes.failed.body']()}
      requestIdLabel={m['error.reference']()}
      requestId={shopping.error?.requestId}
    >
      {#snippet action()}
        <Button
          variant="primary"
          onclick={() => {
            shopping.clearError();

            if (householdId) {
              void shopping.load(householdId);
            }
          }}
        >
          {m['error.retry']()}
        </Button>
      {/snippet}
    </ErrorState>
  {:else if shopping.status === 'ready' && shopping.items.length === 0}
    <EmptyState title={m['shopping.empty.title']()} body={m['shopping.empty.body']()}>
      {#snippet action()}
        <Button href="/">{m['recipes.title']()}</Button>
      {/snippet}
    </EmptyState>
  {:else}
    {#each shopping.toBuy as group (group.section)}
      <section class="section">
        <h2 class="heading">{nameOf(group.section)}</h2>

        <ul class="list">
          {#each group.items as item (item.itemId)}
            <ShoppingItemRow
              {item}
              oncheck={(isChecked) =>
                householdId && shopping.check(householdId, item.itemId, isChecked)}
              onremove={() => householdId && shopping.remove(householdId, item.itemId)}
            />
          {/each}
        </ul>
      </section>
    {/each}

    {#if shopping.bought.length > 0}
      <section class="section bought">
        <h2 class="heading">{m['shopping.bought']({ count: shopping.bought.length })}</h2>

        <ul class="list">
          {#each shopping.bought as item (item.itemId)}
            <ShoppingItemRow
              {item}
              oncheck={(isChecked) =>
                householdId && shopping.check(householdId, item.itemId, isChecked)}
              onremove={() => householdId && shopping.remove(householdId, item.itemId)}
            />
          {/each}
        </ul>
      </section>
    {/if}
  {/if}
</Page>

<style>
  .head {
    display: flex;
    align-items: baseline;
    justify-content: space-between;
    gap: var(--space-4);
    margin-bottom: var(--space-6);
  }

  .title {
    font-family: var(--font-editorial);
    font-size: var(--text-display);
    font-weight: var(--weight-regular);
    letter-spacing: -0.03em;
  }

  .add {
    display: flex;
    gap: var(--space-3);
    margin-bottom: var(--space-8);
    max-width: 32rem;
  }

  .section {
    margin-bottom: var(--space-6);
    max-width: 32rem;
  }

  .heading {
    font-size: var(--text-sm);
    font-weight: var(--weight-semibold);
    letter-spacing: 0.08em;
    text-transform: uppercase;
    color: var(--text-muted);
    margin-bottom: var(--space-2);
    padding-bottom: var(--space-2);
    border-bottom: 1px solid var(--border);
  }

  /* Kept at the bottom and quieter: seeing what is already in the trolley is
     how you know you have not missed anything. */
  .bought {
    margin-top: var(--space-8);
    opacity: 0.7;
  }

  .list {
    margin: 0;
    padding: 0;
    list-style: none;
  }
</style>
