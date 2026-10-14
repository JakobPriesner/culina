<script lang="ts">
  import { resolve } from '$app/paths';
  import { ActionMenu, Checkbox, IconButton } from '$ds';

  import { formatQuantity } from '$features/recipes/formatQuantity';
  import { quantityLabels } from '$features/recipes/quantityLabels';
  import type { MealSlot } from '$features/planning/mealPlan.svelte';
  import { slotLabel } from '$features/planning/slots';
  import { haptics } from '$shell/haptics';
  import { formatDate, m } from '$shell/i18n';
  import { preferences } from '$shell/preferences.svelte';
  import { amountToBuy } from './amountToBuy';
  import { nameOf, sectionOrder, type Section } from './sections';
  import type { ShoppingItem } from './stores/shopping.svelte';

  /**
   * One line, read while walking: the whole row is the tick, and the amount is rounded here, last (the server keeps the exact sum so added recipes do not compound error).
   * The section is a guess, corrected on the line itself and remembered for that name.
   */
  interface Props {
    item: ShoppingItem;
    oncheck: (isChecked: boolean) => void;
    onremove: () => void;
    onmove: (section: Section) => void;
  }

  let { item, oncheck, onremove, onmove }: Props = $props();

  let showingSources = $state(false);

  const amountOf = (quantity: number | null | undefined, unit: string | null | undefined) =>
    formatQuantity(
      amountToBuy({ value: quantity ?? null, unit: unit ?? null }, preferences.measurementSystem),
      preferences.locale,
      quantityLabels
    ).text;

  const amount = $derived(amountOf(item.quantity, item.unit));
  const sources = $derived(item.sources ?? []);
  /** What no recipe accounts for (typed by hand, or left from elsewhere), so the sources add up to the line. */
  const other = $derived(item.otherQuantity ? amountOf(item.otherQuantity, item.unit) : '');

  const plannedFor = (date: string, slot: string | null | undefined) =>
    m['shopping.source.planned']({
      date: formatDate(new Date(`${date}T12:00:00`), {
        weekday: 'short',
        day: 'numeric',
        month: 'short'
      }),
      // A slot this client does not know is left out rather than guessed at.
      slot: (slot && slotLabel[slot as MealSlot]?.()) || ''
    });

  /** Moves the line, but only when the section chosen is a different one. */
  function move(section: Section) {
    if (section !== item.section) {
      onmove(section);
    }
  }
</script>

<li class="row" class:bought={item.isChecked}>
  <div class="check">
    <Checkbox
      checked={item.isChecked}
      label={item.name}
      describedBy={amount ? `shopping-amount-${item.itemId}` : undefined}
      onchange={(isChecked) => {
        haptics.tick();
        oncheck(isChecked);
      }}
    />
  </div>
  {#if amount}<span class="amount" id="shopping-amount-{item.itemId}">{amount}</span>{/if}
  <div class="actions">
    <ActionMenu wrap>
      {#snippet trigger({ popovertarget })}
        <IconButton
          label={m['shopping.itemActions']({ name: item.name })}
          size="sm"
          {popovertarget}
        >
          <svg viewBox="0 0 24 24" fill="currentColor">
            <circle cx="5" cy="12" r="1.6" /><circle cx="12" cy="12" r="1.6" /><circle
              cx="19"
              cy="12"
              r="1.6"
            />
          </svg>
        </IconButton>
      {/snippet}
      {#if sources.length > 0}
        <button
          class="item"
          type="button"
          aria-expanded={showingSources}
          onclick={() => (showingSources = !showingSources)}
        >
          {showingSources
            ? m['shopping.sources.hide']()
            : m['shopping.sources.show']({ count: sources.length })}
        </button>
      {/if}
      <button type="button" class="item danger" onclick={onremove}
        >{m['shopping.remove']({ name: item.name })}</button
      >
      {#if !item.isChecked}
        <hr class="separator" />
        <p class="heading">{m['shopping.move.heading']()}</p>
        {#each sectionOrder as section (section)}
          {@const current = section === item.section}
          <button
            type="button"
            class="item"
            aria-current={current || undefined}
            onclick={() => move(section)}
          >
            <span class="mark" aria-hidden="true">
              {#if current}
                <svg
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke="currentColor"
                  stroke-width="2"
                  stroke-linecap="round"
                  stroke-linejoin="round"><path d="m5 12 5 5 9-10" /></svg
                >
              {/if}
            </span>
            {nameOf(section)}
          </button>
        {/each}
      {/if}
    </ActionMenu>
  </div>
  {#if item.isListedSeparately && !item.isChecked}
    <p class="separate">{m['shopping.separate']()}</p>
  {/if}
  {#if showingSources}
    <ul class="sources">
      {#each sources as source, index (`${source.recipeId}-${source.plannedDate ?? 'direct'}-${index}`)}
        <li class="source">
          <a href={resolve('/(app)/recipes/[recipeId]', { recipeId: source.recipeId })}
            >{source.recipeTitle}</a
          >
          {#if amountOf(source.quantity, source.unit)}<span class="source-amount"
              >{amountOf(source.quantity, source.unit)}</span
            >{/if}
          {#if source.plannedDate}<span class="source-plan"
              >{plannedFor(source.plannedDate, source.plannedSlot)}</span
            >{/if}
        </li>
      {/each}
      {#if other}
        <li class="source">
          <span class="source-name">{m['shopping.source.other']()}</span>
          <span class="source-amount">{other}</span>
        </li>
      {/if}
    </ul>
  {/if}
</li>

<style>
  .row {
    display: grid;
    grid-template-columns: minmax(0, 1fr) auto auto;
    align-items: center;
    column-gap: var(--space-2);
    min-height: var(--control-lg);
    border-bottom: 1px solid var(--border);
    transition: background-color var(--duration-fast) var(--ease-out);
  }
  .row:hover {
    background: var(--surface-hover);
  }
  .check {
    min-width: 0;
    padding-block: var(--space-1);
    overflow-wrap: anywhere;
  }
  .check :global(label) {
    width: 100%;
  }
  .bought .check,
  .bought .amount {
    color: var(--text-subtle);
    text-decoration: line-through;
  }
  .amount {
    grid-column: 2;
    color: var(--text-muted);
    font-size: var(--text-xs);
    font-variant-numeric: tabular-nums;
    white-space: nowrap;
  }
  .actions {
    grid-column: 3;
  }
  /* Too narrow to share a line: the amount drops under the name, so the name keeps its whole words. */
  @media (width < 24rem) {
    .row {
      grid-template-columns: minmax(0, 1fr) auto;
    }
    .amount {
      grid-column: 1;
      grid-row: 2;
      padding: 0 0 var(--space-2) calc(var(--space-6) + var(--space-3));
    }
    .actions {
      grid-column: 2;
      grid-row: 1;
    }
  }
  .sources {
    display: flex;
    grid-column: 1 / -1;
    flex-direction: column;
    gap: var(--space-2);
    margin: 0;
    padding: var(--space-2) 0 var(--space-4) calc(var(--space-6) + var(--space-3));
    list-style: none;
  }
  .source {
    display: flex;
    flex-wrap: wrap;
    align-items: baseline;
    gap: var(--space-2);
    color: var(--text-muted);
    font-size: var(--text-sm);
  }
  .source a,
  .source-name {
    color: var(--text);
    font-weight: var(--weight-medium);
  }
  .separate {
    grid-column: 1 / -1;
    margin: 0;
    padding: 0 0 var(--space-2) calc(var(--space-6) + var(--space-3));
    color: var(--text-muted);
    font-size: var(--text-xs);
  }
  .source-amount {
    font-variant-numeric: tabular-nums;
  }
  .source-plan {
    color: var(--text-subtle);
  }
</style>
