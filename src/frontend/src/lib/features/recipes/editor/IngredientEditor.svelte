<script lang="ts">
  import { IconButton } from '$ds';

  import { m } from '$shell/i18n';
  import { parseIngredientLine } from './parseIngredientLine';
  import { formatQuantity } from '../formatQuantity';
  import { quantityLabels } from '../quantityLabels';
  import { scaleQuantity } from '../scaling';
  import { preferences } from '$shell/preferences.svelte';
  import type { Ingredient } from '../types';

  /**
   * The ingredient list, written one line at a time.
   *
   * Type "200 g Mehl", press Enter, type the next. The parse is shown back as
   * separate parts — that visibility is the whole reason the shortcut is
   * trustworthy: a wrong read is obvious, and removing a line is one tap.
   */
  interface Props {
    ingredients: readonly Ingredient[];
    onchange: (ingredients: Ingredient[]) => void;
  }

  let { ingredients, onchange }: Props = $props();

  let line = $state('');
  let input = $state<HTMLInputElement>();

  const shown = (ingredient: Ingredient) =>
    formatQuantity(scaleQuantity(ingredient.quantity, 1), preferences.locale, quantityLabels).text;

  function add() {
    const text = line.trim();

    if (!text) {
      return;
    }

    const parsed = parseIngredientLine(text);

    onchange([
      ...ingredients,
      // No id: the server assigns one, and a line that has never been saved has
      // no identity to borrow.
      { id: '', quantity: parsed.quantity, name: parsed.name, note: parsed.note }
    ]);

    line = '';
    input?.focus();
  }

  function remove(index: number) {
    onchange(ingredients.filter((_, candidate) => candidate !== index));
  }
</script>

<div class="editor">
  <ul class="list">
    {#each ingredients as ingredient, index (index)}
      <li class="row">
        <span class="amount">{shown(ingredient)}</span>

        <span class="name">
          {ingredient.name}{#if ingredient.note}<span class="note">, {ingredient.note}</span>{/if}
        </span>

        <IconButton
          label={m['editor.removeIngredient']({ name: ingredient.name })}
          size="sm"
          onclick={() => remove(index)}
        >
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <path d="m6 6 12 12M18 6 6 18" stroke-linecap="round" />
          </svg>
        </IconButton>
      </li>
    {/each}
  </ul>

  <!-- Enter adds the line and puts the cursor back, so a whole list can be
       typed without ever reaching for the mouse. -->
  <input
    bind:this={input}
    class="ds-control"
    id="ingredient-line"
    type="text"
    bind:value={line}
    aria-label={m['editor.ingredientLine']()}
    placeholder={m['editor.ingredientHint']()}
    onkeydown={(event) => {
      if (event.key === 'Enter') {
        event.preventDefault();
        add();
      }
    }}
  />
</div>

<style>
  .editor {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
  }

  .list {
    margin: 0;
    padding: 0;
    list-style: none;
  }

  .row {
    display: grid;
    grid-template-columns: minmax(4rem, auto) 1fr auto;
    align-items: center;
    gap: var(--space-3);
    padding-block: var(--space-1);
  }

  .amount {
    font-variant-numeric: tabular-nums;
    font-weight: var(--weight-medium);
    white-space: nowrap;
  }

  .note {
    color: var(--text-muted);
  }
</style>
