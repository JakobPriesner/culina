<script lang="ts">
  import { Button, EmptyState, ErrorState } from '$ds';
  import IngredientFields, {
    emptyDraft,
    toIngredient,
    type IngredientDraft
  } from '$features/recipes/editor/IngredientFields.svelte';
  import { unitFor } from '$features/recipes/quantityLabels';
  import RecipePicker from '$features/recipes/RecipePicker.svelte';
  import { units } from '$features/recipes/stores/units.svelte';
  import type { RecipeSummary } from '$features/recipes/types';
  import { session } from '$features/auth/session.svelte';
  import { nameOf } from '$features/shopping/sections';
  import ShoppingItemRow from '$features/shopping/ShoppingItemRow.svelte';
  import ShoppingListSkeleton from '$features/shopping/ShoppingListSkeleton.svelte';
  import { shopping } from '$features/shopping/stores/shopping.svelte';
  import { explain } from '$shell/explain';
  import { toaster } from '$shell/toaster.svelte';
  import { m } from '$shell/i18n';
  import Page from '$shell/Page.svelte';
  import { preferences } from '$shell/preferences.svelte';

  /**
   * The list, as it is actually used: standing in a shop, one hand free.
   *
   * Sections in the order a shop is walked, what is already in the trolley kept
   * visible at the bottom, and one bulk action — clearing what is bought —
   * because after a shop removing a dozen lines one at a time is the tedium
   * this exists to avoid.
   */
  let typed = $state<IngredientDraft>(emptyDraft);

  /** Whether the recipe picker is up. */
  let picking = $state(false);

  /** Which recipes this opening of the picker has already put on. */
  let taken = $state<string[]>([]);

  const householdId = $derived(session.activeHouseholdId);

  /**
   * How many lines are still to find, which is the number a shopper wants.
   *
   * Said in the subtitle only while it is a number worth having. "0 still to
   * buy" is a count of nothing, and the finished list says so in words
   * further down, where the list itself would have been.
   */
  const remaining = $derived(shopping.items.length - shopping.bought.length);

  /** Nothing left to find, but the trolley is not empty: a finished shop. */
  const finished = $derived(shopping.toBuy.length === 0 && shopping.bought.length > 0);

  $effect(() => {
    if (householdId) {
      void shopping.load(householdId);
      void units.load(householdId);
    }
  });

  /**
   * One line in, written the way an ingredient is written into a recipe.
   *
   * The same three fields as the editor, and the same component: an amount, a
   * unit and a name are three things, and a single box that has to be taken
   * apart afterwards guesses at where each one ends. It also means the unit
   * this household invented and the names already in its recipes are offered
   * here too — a shopping list is mostly words it has seen before.
   */
  async function add() {
    const line = toIngredient(typed, '');

    // The name is the item. An amount with nothing to measure is not a
    // half-finished line worth keeping, it is a line that says nothing.
    if (!line.name || !householdId) {
      return;
    }

    // Kept on settling rather than per keystroke, so writing "Schuss" does not
    // leave S, Sc and Sch behind as units this kitchen measures in.
    const unit = unitFor(typed.unit);

    if (unit) {
      units.remember(unit);
    }

    typed = emptyDraft;
    document.getElementById('shopping-add-amount')?.focus();

    await shopping.add(
      householdId,
      line.name,
      line.quantity.value ?? undefined,
      line.quantity.unit
    );
  }

  /**
   * A whole recipe in, at the yield it is written for.
   *
   * The other half of how a list fills up. Typing a line at a time is for the
   * milk and the washing-up liquid; a recipe is twelve lines nobody wants to
   * copy out, and the server merges them into what is already there.
   *
   * Its own yield rather than a number asked for here: the amounts a recipe
   * states are the ones somebody meant, and the recipe page is where a
   * different number is chosen — with every quantity on screen to check it
   * against, which is the part a picker row cannot show.
   *
   * The sheet stays up. A week's list is four or five recipes, and closing
   * after each one charged the search box, the scroll and the reopening to
   * every recipe after the first. The row saying it has been taken is the
   * receipt — a toast could not be one, because the sheet is a native dialog
   * and sits above it.
   */
  async function addRecipe(recipe: RecipeSummary) {
    if (!householdId || taken.includes(recipe.id)) {
      return;
    }

    // Said before the round trip finishes. The list underneath updates when it
    // does, and a row that waits half a second to admit it was pressed is a
    // row somebody presses twice.
    taken = [...taken, recipe.id];

    const failure = await shopping.addRecipe(householdId, recipe.id, recipe.yieldAmount);

    if (!failure) {
      return;
    }

    // Out of the way, so the reason is readable: nothing may cover a dialog.
    taken = taken.filter((id) => id !== recipe.id);
    picking = false;

    toaster.show({ message: explain(failure), tone: 'danger' });
  }

  function stopPicking() {
    picking = false;
    taken = [];
  }
</script>

<svelte:head><title>{m['shopping.title']()}</title></svelte:head>

<Page width="reading">
  <header class="head">
    <div class="titles">
      <h1 class="title">{m['shopping.title']()}</h1>
      <p class="subtitle">
        {#if remaining > 0}
          {m['shopping.remaining']({ count: remaining })}
        {:else}
          {m['shopping.subtitle']()}
        {/if}
      </p>
    </div>

    <!-- Beside the title rather than among the fields below it: both fill the
         list, but one line and twelve are different enough acts that putting
         their controls together makes the wrong one easy to hit. -->
    <Button onclick={() => (picking = true)}>+ {m['shopping.addRecipe']()}</Button>
  </header>

  <div class="adding">
    <!-- Enter adds the line and puts the cursor back on the amount, so a whole
         list can be written without ever reaching for the mouse. -->
    <div class="add">
      <IngredientFields
        id="shopping-add"
        label={m['shopping.addFields']()}
        nameLabel={m['shopping.itemName']()}
        note={false}
        value={typed}
        onchange={(draft) => (typed = draft)}
        onsubmit={add}
        householdId={householdId ?? ''}
        language={preferences.locale}
      />

      <Button variant="primary" onclick={add} disabled={!typed.name.trim()}>
        {m['shopping.add']()}
      </Button>
    </div>

    <p class="hint">{m['shopping.addHint']()}</p>
  </div>

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
  {:else if shopping.status === 'loading' && shopping.items.length === 0}
    <!-- The shape of a list rather than a spinner, so the page does not sit
         blank and then jump. -->
    <ShoppingListSkeleton />
  {:else if shopping.status === 'ready' && shopping.items.length === 0}
    <EmptyState title={m['shopping.empty.title']()} body={m['shopping.empty.body']()}>
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
      <section class="section">
        <h2 class="heading-row"><span class="label">{nameOf(group.section)}</span></h2>

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

    <!-- The shop is done. Said out loud, because a page whose only content is
         a struck-through list looks like a page that has lost something. -->
    {#if finished}
      <p class="finished">{m['shopping.allBought']()}</p>
    {/if}

    {#if shopping.bought.length > 0}
      <section class="section bought">
        <h2 class="heading-row">
          <span class="label">{m['shopping.bought']({ count: shopping.bought.length })}</span>

          <!-- Next to what it clears. At the top of the page it was an action
               with nothing near it to explain what it would take away. -->
          <Button size="sm" onclick={() => householdId && shopping.clearBought(householdId)}>
            {m['shopping.clearBought']()}
          </Button>
        </h2>

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

{#if householdId}
  <RecipePicker
    open={picking}
    {householdId}
    title={m['shopping.pick.title']()}
    {taken}
    onpick={(recipe) => void addRecipe(recipe)}
    onclose={stopPicking}
  >
    <!-- One way out that reads as finishing rather than abandoning, since by
         now the sheet is a list of things already done. -->
    {#snippet footer()}
      <Button variant="primary" onclick={stopPicking}>{m['picker.done']()}</Button>
    {/snippet}
  </RecipePicker>
{/if}

<style>
  .head {
    display: flex;
    flex-wrap: wrap;
    align-items: flex-end;
    justify-content: space-between;
    gap: var(--space-4);
    margin-bottom: var(--space-6);
  }

  .titles {
    min-width: 0;
  }

  .title {
    font-family: var(--font-editorial);
    font-size: var(--text-display);
    font-weight: var(--weight-regular);
    letter-spacing: -0.03em;
  }

  .subtitle {
    color: var(--text-muted);
    font-size: var(--text-sm);
    margin-top: var(--space-2);
  }

  .adding {
    container: shopping-entry / inline-size;
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
    padding-block: var(--space-6);
    border-block: 1px solid var(--border);
    margin-bottom: var(--space-8);
  }

  .add {
    display: grid;
    grid-template-columns: minmax(0, 1fr) auto;
    align-items: end;
    gap: var(--space-3);
  }

  .hint {
    color: var(--text-muted);
    font-size: var(--text-xs);
  }

  /* On a phone the fields already stack, and a button beside them would have
     nothing but a sliver left. It goes underneath instead. */
  @container shopping-entry (width < 40rem) {
    .add {
      grid-template-columns: 1fr;
    }
  }

  .section {
    margin-bottom: var(--space-8);
  }

  .heading-row {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-3);
    margin-bottom: var(--space-3);
  }

  .label {
    font-size: var(--text-xs);
    font-weight: var(--weight-semibold);
    letter-spacing: 0.14em;
    text-transform: uppercase;
    color: var(--text-muted);
  }

  .finished {
    color: var(--text-muted);
    font-size: var(--text-sm);
    margin-bottom: var(--space-8);
  }

  /* Kept at the bottom and set apart rather than dimmed as a block: opacity on
     the whole section takes its own button's contrast down with it. The rows
     say what they are on their own — struck through, and quieter. */
  .bought {
    margin-top: var(--space-12);
    padding-top: var(--space-6);
    border-top: 1px solid var(--border);
  }

  .list {
    margin: 0;
    padding: 0;
    list-style: none;
  }
</style>
