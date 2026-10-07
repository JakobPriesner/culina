<script lang="ts">
  import { resolve } from '$app/paths';
  import { ActionMenu, Checkbox, IconButton } from '$ds';

  import { formatQuantity } from '$features/recipes/formatQuantity';
  import { quantityLabels } from '$features/recipes/quantityLabels';
  import { scaleQuantity } from '$features/recipes/scaling';
  import type { MealSlot } from '$features/planning/mealPlan.svelte';
  import { slotLabel } from '$features/planning/slots';
  import { haptics } from '$shell/haptics';
  import { formatDate, m } from '$shell/i18n';
  import { preferences } from '$shell/preferences.svelte';
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
  <Checkbox
    checked={item.isChecked}
    label={item.name}
    onchange={(isChecked) => {
      haptics.tick();
      oncheck(isChecked);
    }}
  />

  <!-- Beside the name, not at the far edge: ranged right across a wide column the amount sits an inch from its name; read as a phrase ("cherry tomatoes, 250 g") it is one glance. -->
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

  <span class="actions">
    <!-- Only while still to find: a bought line is in no aisle. -->
    {#if !item.isChecked}
      <ActionMenu wrap>
        {#snippet trigger({ popovertarget })}
          <IconButton label={m['shopping.move']({ name: item.name })} size="sm" {popovertarget}>
            <!-- Two arrows passing: this goes somewhere else, not away. -->
            <svg
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="2"
              stroke-linecap="round"
              stroke-linejoin="round"
            >
              <path d="M7 4v16M3 8l4-4 4 4M17 20V4M13 16l4 4 4-4" />
            </svg>
          </IconButton>
        {/snippet}

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
                  stroke-linejoin="round"
                >
                  <path d="m5 12 5 5 9-10" />
                </svg>
              {/if}
            </span>
            {nameOf(section)}
          </button>
        {/each}
      </ActionMenu>
    {/if}

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
    /* Floor for a thumb target on a moving trolley; stops two short lines landing closer together. */
    min-height: var(--control-sm);
    /* Bled to the gutter and back so hover lands on the line, not the words. Fixed pixels, not a rem step: at 200% text a rem bleed pushes the row past a 320px screen. */
    margin-inline: -12px;
    padding-inline: 12px;
    border-radius: var(--radius-md);
    transition: background-color var(--duration-fast) var(--ease-out);
  }

  .row:hover {
    background: var(--surface-hover);
  }

  /* Struck through and dimmed, not removed: seeing what is in the trolley shows nothing was missed. */
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

  /* Held back until the line is reached: a column of crosses reads as the thing to press, and removing cannot be undone; moving waits too. */
  .actions {
    display: inline-flex;
    align-items: center;
    opacity: 0;
    transition: opacity var(--duration-fast) var(--ease-out);
  }

  .row:hover .actions,
  .actions:focus-within,
  .actions:has(:popover-open) {
    opacity: 1;
  }

  /* A finger has no hover, so on touch there is nothing to reveal it with. */
  @media (hover: none) {
    .actions {
      opacity: 1;
    }
  }

  @media (width < 32rem) {
    .row {
      grid-template-columns: minmax(0, 1fr) auto auto;
    }

    .row > :global(:first-child) {
      grid-column: 1 / -1;
    }

    .amount {
      padding-inline-start: calc(var(--control-sm) + var(--space-3));
    }
  }
</style>
