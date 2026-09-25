<script lang="ts">
  import { resolve } from '$app/paths';
  import { Checkbox, IconButton } from '$ds';

  import { formatQuantity } from '$features/recipes/formatQuantity';
  import { quantityLabels } from '$features/recipes/quantityLabels';
  import { scaleQuantity } from '$features/recipes/scaling';
  import { m } from '$shell/i18n';
  import { preferences } from '$shell/preferences.svelte';
  import type { ShoppingItem } from './stores/shopping.svelte';

  /**
   * One line, in a shop, held one-handed.
   *
   * The whole row is the tick: a checkbox alone is a 16px target, and this is
   * read while walking. The amount is rounded here, at the last possible
   * moment — the server stores the exact sum so that adding three recipes does
   * not compound rounding error.
   */
  interface Props {
    item: ShoppingItem;
    oncheck: (isChecked: boolean) => void;
    onremove: () => void;
  }

  let { item, oncheck, onremove }: Props = $props();

  let showingSources = $state(false);

  const amountOf = (quantity: number | null | undefined, unit: string | null | undefined) =>
    formatQuantity(
      scaleQuantity(
        { value: quantity ?? null, unit: unit ?? null },
        1,
        preferences.measurementSystem
      ),
      preferences.locale,
      quantityLabels
    ).text;

  const amount = $derived(amountOf(item.quantity, item.unit));
  const sources = $derived(item.sources ?? []);
  const dates = $derived(
    new Intl.DateTimeFormat(preferences.locale, {
      weekday: 'short',
      day: 'numeric',
      month: 'short'
    })
  );

  const plannedFor = (date: string, slot: string | null | undefined) =>
    m['shopping.source.planned']({
      date: dates.format(new Date(`${date}T12:00:00`)),
      slot: slot ? m[`plan.slot.${slot as 'breakfast' | 'lunch' | 'dinner'}`]() : ''
    });
</script>

<li class="row" class:bought={item.isChecked}>
  <Checkbox
    checked={item.isChecked}
    label={item.name}
    onchange={(isChecked) => oncheck(isChecked)}
  />

  <!-- Beside the name rather than flushed to the far edge. Ranged right across
       a column this wide, "Zucchini" and "3" end up an inch apart with nothing
       between them, and the eye has to travel the gap for every line. Read as
       a phrase — "cherry tomatoes, 250 g" — it is one glance. -->
  {#if amount}
    <span class="amount">{amount}</span>
  {/if}

  {#if sources.length > 0}
    <button
      class="source-toggle"
      type="button"
      aria-expanded={showingSources}
      onclick={() => (showingSources = !showingSources)}
    >
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
        <path d="m9 6 6 6-6 6" stroke-linecap="round" stroke-linejoin="round" />
      </svg>
      {showingSources
        ? m['shopping.sources.hide']()
        : m['shopping.sources.show']({ count: sources.length })}
    </button>
  {/if}

  <span class="remove">
    <IconButton label={m['shopping.remove']({ name: item.name })} size="sm" onclick={onremove}>
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
        <path d="m6 6 12 12M18 6 6 18" stroke-linecap="round" />
      </svg>
    </IconButton>
  </span>

  {#if showingSources}
    <ul class="sources">
      {#each sources as source, index (`${source.recipeId}-${source.plannedDate ?? 'direct'}-${index}`)}
        <li class="source">
          <a href={resolve('/(app)/recipes/[recipeId]', { recipeId: source.recipeId })}>
            {source.recipeTitle}
          </a>

          {#if amountOf(source.quantity, source.unit)}
            <span class="source-amount">{amountOf(source.quantity, source.unit)}</span>
          {/if}

          {#if source.plannedDate}
            <span class="source-plan">{plannedFor(source.plannedDate, source.plannedSlot)}</span>
          {/if}
        </li>
      {/each}
    </ul>
  {/if}
</li>

<style>
  .row {
    display: grid;
    grid-template-columns: minmax(0, auto) minmax(0, 1fr) auto auto;
    align-items: center;
    gap: var(--space-3);
    /* The floor for something a thumb has to hit while a trolley is moving.
       The tick inside the row is already that tall; this is what stops two
       lines of short words from landing closer together than that. */
    min-height: var(--control-sm);
    /* Bled out to the gutter and back so the hover lands on the whole line
       rather than on the words: the line is the target, not the text.
       Fixed pixels, not a spacing step: the step is in rem, so at 200% text
       this bleeds 24px each side into a gutter that did not grow with it, and
       the row reaches past the edge of a 320px screen. How far a hover target
       extends past its text is not a question about how big the text is. */
    margin-inline: -12px;
    padding-inline: 12px;
    border-radius: var(--radius-md);
    transition: background-color var(--duration-fast) var(--ease-out);
  }

  .row:hover {
    background: var(--surface-hover);
  }

  /* Struck through and dimmed rather than removed: seeing what is already in
     the trolley is how you know you have not missed anything. */
  .bought {
    color: var(--text-subtle);
    text-decoration: line-through;
  }

  .amount {
    justify-self: start;
    color: var(--text-muted);
    font-size: var(--text-sm);
    font-variant-numeric: tabular-nums;
    white-space: nowrap;
  }

  .bought .amount {
    color: inherit;
  }

  .source-toggle {
    display: inline-flex;
    align-items: center;
    gap: var(--space-1);
    min-height: var(--control-sm);
    padding: 0 var(--space-2);
    border: none;
    border-radius: var(--radius-md);
    background: none;
    color: var(--text-muted);
    font: inherit;
    font-size: var(--text-xs);
    cursor: pointer;
  }

  .source-toggle:hover {
    background: var(--surface-selected);
    color: var(--text);
  }

  .source-toggle svg {
    width: var(--space-4);
    height: var(--space-4);
    transition: transform var(--duration-fast) var(--ease-out);
  }

  .source-toggle[aria-expanded='true'] svg {
    transform: rotate(90deg);
  }

  .sources {
    display: flex;
    grid-column: 1 / -1;
    flex-direction: column;
    gap: var(--space-2);
    margin: 0;
    padding: var(--space-2) var(--space-3) var(--space-3) calc(var(--control-sm) + var(--space-3));
    border-top: 1px solid var(--border);
    list-style: none;
    text-decoration: none;
  }

  .source {
    display: flex;
    flex-wrap: wrap;
    align-items: baseline;
    gap: var(--space-2);
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .source a {
    color: var(--text);
    font-weight: var(--weight-medium);
  }

  .source-amount {
    font-variant-numeric: tabular-nums;
  }

  .source-plan {
    color: var(--text-subtle);
  }

  /* Held back until the line is reached. A column of crosses down the edge of
     a list reads as the thing to press, and it is the one action here that
     cannot be undone. */
  .remove {
    opacity: 0;
    transition: opacity var(--duration-fast) var(--ease-out);
  }

  .row:hover .remove,
  .remove:focus-within {
    opacity: 1;
  }

  /* A finger has no hover, so on touch there is nothing to reveal it with. */
  @media (hover: none) {
    .remove {
      opacity: 1;
    }
  }

  @media (width < 32rem) {
    .row {
      grid-template-columns: minmax(0, 1fr) auto auto;
    }

    .row :global(.ds-checkbox) {
      grid-column: 1 / -1;
    }

    .amount {
      padding-inline-start: calc(var(--control-sm) + var(--space-3));
    }
  }
</style>
