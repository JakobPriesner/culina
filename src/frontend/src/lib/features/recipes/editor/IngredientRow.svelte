<script lang="ts">
  import { IconButton } from '$ds';
  import { m } from '$shell/i18n';
  import { preferences } from '$shell/preferences.svelte';

  import { formatQuantity } from '../formatQuantity';
  import { quantityLabels } from '../quantityLabels';
  import { scaleQuantity } from '../scaling';
  import type { Ingredient } from '../types';
  import IngredientFields, { type IngredientDraft } from './IngredientFields.svelte';

  // Steps are linked read-only here; attaching ingredients happens on the steps only, so the two can't disagree.
  interface Props {
    ingredient: Ingredient;
    index: number;
    /** When open, `draft` is what is being written. */
    open: boolean;
    draft: IngredientDraft;
    usedIn: readonly number[];
    householdId: string;
    language: string;
    oncorrect: (draft: IngredientDraft) => void;
    onclose: () => void;
    ontoggle: () => void;
    onremove: () => void;
  }

  let {
    ingredient,
    index,
    open,
    draft,
    usedIn,
    householdId,
    language,
    oncorrect,
    onclose,
    ontoggle,
    onremove
  }: Props = $props();

  const goTo = (number: number) => document.getElementById(`step-${number - 1}`)?.focus();

  const shown = (ingredient: Ingredient) =>
    formatQuantity(scaleQuantity(ingredient.quantity, 1), preferences.locale, quantityLabels).text;
</script>

<li class="row" class:open>
  {#if open}
    <div class="fields">
      <IngredientFields
        id="ingredient-{index}"
        label={m['editor.editIngredient']({ name: ingredient.name })}
        value={draft}
        onchange={oncorrect}
        onsubmit={onclose}
        {householdId}
        {language}
      />
    </div>
  {:else}
    <span class="amount">{shown(ingredient)}</span>

    <div class="name">
      <span class="written"
        >{ingredient.name}{#if ingredient.note}<span class="note">, {ingredient.note}</span
          >{/if}</span
      >

      {#if ingredient.id}
        <span class="where">
          {#if usedIn.length > 0}
            <!-- A preposition, so the line reads right for one number or four. -->
            {m['editor.usedInSteps']()}
            {#each usedIn as number, at (number)}
              {#if at > 0},
              {/if}<button
                type="button"
                class="jump"
                aria-label={m['editor.goToStep']({ number })}
                onclick={() => goTo(number)}>{number}</button
              >
            {/each}
          {:else}
            {m['editor.notUsedInAStep']()}
          {/if}
        </span>
      {/if}
    </div>
  {/if}

  <div class="controls">
    <IconButton
      label={open
        ? m['editor.doneWithIngredient']({ name: ingredient.name })
        : m['editor.editIngredient']({ name: ingredient.name })}
      size="sm"
      onclick={ontoggle}
    >
      {#if open}
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <path d="m5 13 4 4 10-10" stroke-linecap="round" stroke-linejoin="round" />
        </svg>
      {:else}
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <path
            d="M4 20h4L19 9a2.1 2.1 0 0 0-3-3L5 17v3Z"
            stroke-linecap="round"
            stroke-linejoin="round"
          />
        </svg>
      {/if}
    </IconButton>

    <IconButton
      label={m['editor.removeIngredient']({ name: ingredient.name })}
      size="sm"
      onclick={onremove}
    >
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
        <path d="m6 6 12 12M18 6 6 18" stroke-linecap="round" />
      </svg>
    </IconButton>
  </div>
</li>

<style>
  .row {
    display: grid;
    grid-column: 1 / -1;
    grid-template-columns: subgrid;
    align-items: center;
    padding: var(--space-2) var(--space-4);
    transition: background-color var(--duration-fast) var(--ease-out);
  }

  .row:hover {
    background: var(--surface-hover);
  }

  .name {
    min-width: 0;
  }

  /* Muted, not a warning: an ingredient in no step is normal. */
  .where {
    display: block;
    color: var(--text-subtle);
    font-size: var(--text-xs);
  }

  .jump {
    padding: 0;
    border: none;
    background: none;
    color: inherit;
    font: inherit;
    text-decoration: underline;
    text-decoration-color: var(--border-strong);
    cursor: pointer;
  }

  .jump:hover {
    color: var(--accent);
  }

  .row.open {
    grid-template-columns: minmax(0, 1fr) auto;
    align-items: end;
    padding-block: var(--space-4);
    background: var(--surface-sunken);
  }

  .row.open .fields {
    min-width: 0;
  }

  .controls {
    display: flex;
    gap: var(--space-1);
  }

  /* Controls fade in on hover/focus so the list doesn't read as a toolbar; on touch they stay visible. */
  @media (hover: hover) {
    .row:not(.open) .controls {
      opacity: 0;
      transition: opacity var(--duration-fast) var(--ease-out);
    }

    .row:hover .controls,
    .row:focus-within .controls {
      opacity: 1;
    }
  }

  .amount {
    font-variant-numeric: tabular-nums;
    font-weight: var(--weight-medium);
    white-space: nowrap;
  }

  .note {
    color: var(--text-muted);
  }

  /* Keeps the name readable in a narrow editor or with enlarged text. */
  @container ingredient-editor (width < 24rem) {
    .row:not(.open) .name {
      grid-column: 1 / -1;
      grid-row: 2;
    }

    .row:not(.open) .controls {
      grid-column: 2;
      grid-row: 1;
    }

    .amount {
      white-space: normal;
    }
  }

  @container ingredient-editor (width < 44rem) {
    .row.open {
      grid-template-columns: 1fr;
    }

    .row.open .controls {
      justify-content: flex-end;
    }
  }
</style>
