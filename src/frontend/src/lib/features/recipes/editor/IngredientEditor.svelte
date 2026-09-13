<script lang="ts">
  import { IconButton } from '$ds';

  import { m } from '$shell/i18n';
  import SuggestionList, { type Suggestion } from './SuggestionList.svelte';
  import { parseIngredientLine } from './parseIngredientLine';
  import { formatQuantity } from '../formatQuantity';
  import { quantityLabels } from '../quantityLabels';
  import { scaleQuantity } from '../scaling';
  import { nameOf, type Section } from '$features/shopping/sections';
  import { ingredients as known } from '../stores/ingredients.svelte';
  import { units } from '../stores/units.svelte';
  import { preferences } from '$shell/preferences.svelte';
  import type { Ingredient } from '../types';

  /**
   * The ingredient list, written one line at a time.
   *
   * Type "200 g Mehl", press Enter, type the next. The parse is shown back as
   * separate parts — that visibility is the whole reason the shortcut is
   * trustworthy: a wrong read is obvious, and one tap opens the parts to fix.
   *
   * The unit field is a list you can also type into, which is what makes the
   * vocabulary open. The thirteen built-in units are offered, this kitchen's
   * own are offered after them, and a unit nobody has written before is added
   * by writing it. Once written it is read back from the line from then on, so
   * "1 Schuss Milch" only ever has to be explained once.
   */
  interface Props {
    ingredients: readonly Ingredient[];
    onchange: (ingredients: Ingredient[]) => void;
    /** Whose kitchen, so the suggestions are this household's own words. */
    householdId: string;
    /**
     * What the recipe is written in.
     *
     * The recipe's language, not the reader's: somebody with an English app
     * writing down their grandmother's German recipe wants "Kartoffeln"
     * suggested, and an English word in that ingredient list is a word nothing
     * else in it will match.
     */
    language: string;
  }

  let { ingredients, onchange, householdId, language }: Props = $props();

  let line = $state('');
  let input = $state<HTMLInputElement>();
  /** Which row is open for correction, by position. Only ever one. */
  let editing = $state<number | null>(null);
  let highlighted = $state(0);
  /** Closed until the next keystroke, after choosing or pressing Escape. */
  let dismissed = $state('');

  const listId = 'ingredient-suggestions';

  /**
   * Where the ingredient's name sits inside the line.
   *
   * The line is an amount, a unit and a name, and only the last of those is
   * worth suggesting: nobody needs help typing "200 g". The parser already
   * knows which words are the name, and the name is a suffix of what comes
   * before the comma, so finding it again is a search from the end.
   */
  const span = $derived.by(() => {
    const parsed = parseIngredientLine(line, units.own);
    const at = parsed.name ? line.lastIndexOf(parsed.name) : -1;

    return at === -1 ? null : { at, text: parsed.name };
  });

  const options = $derived<readonly Suggestion[]>(
    known.items
      .filter((one) => one.name.toLowerCase() !== span?.text.toLowerCase())
      .map((one) => ({
        value: one.name,
        label: one.name,
        // Where it lives in a shop, which is the one thing about an ingredient
        // that is useful to know before you have finished typing its name.
        detail: one.own ? undefined : nameOf(one.section as Section)
      }))
  );

  const open = $derived(options.length > 0 && span !== null && line !== dismissed);

  // Asked for on a pause rather than on a keystroke: a request per letter is a
  // request per letter, and the answers come back out of order anyway.
  $effect(() => {
    const wanted = span?.text ?? '';

    if (!wanted) {
      known.clear();

      return;
    }

    const timer = setTimeout(() => void known.suggest(householdId, wanted, language), 180);

    return () => clearTimeout(timer);
  });

  function choose(index: number) {
    const chosen = options[index];

    if (!chosen || !span) {
      return;
    }

    line = line.slice(0, span.at) + chosen.value + line.slice(span.at + span.text.length);
    dismissed = line;
    input?.focus();
  }

  function onkeydown(event: KeyboardEvent) {
    if (event.key === 'Enter' && !open) {
      event.preventDefault();
      add();

      return;
    }

    if (!open) {
      return;
    }

    switch (event.key) {
      case 'ArrowDown':
        event.preventDefault();
        highlighted = (highlighted + 1) % options.length;
        break;
      case 'ArrowUp':
        event.preventDefault();
        highlighted = (highlighted - 1 + options.length) % options.length;
        break;
      case 'Tab':
        event.preventDefault();
        choose(highlighted);
        break;
      case 'Enter':
        // Enter finishes the line, as it always has. Choosing a suggestion is
        // Tab, so somebody who is already typing the right word is never
        // stopped by a list agreeing with them.
        event.preventDefault();
        add();
        break;
      case 'Escape':
        event.preventDefault();
        event.stopPropagation();
        dismissed = line;
        break;
    }
  }

  const shown = (ingredient: Ingredient) =>
    formatQuantity(scaleQuantity(ingredient.quantity, 1), preferences.locale, quantityLabels).text;

  function add() {
    const text = line.trim();

    if (!text) {
      return;
    }

    const parsed = parseIngredientLine(text, units.own);

    onchange([
      ...ingredients,
      // No id: the server assigns one, and a line that has never been saved has
      // no identity to borrow.
      { id: '', quantity: parsed.quantity, name: parsed.name, note: parsed.note }
    ]);

    line = '';
    dismissed = '';
    highlighted = 0;
    known.clear();
    input?.focus();
  }

  function replace(index: number, patch: Partial<Ingredient>) {
    onchange(ingredients.map((one, at) => (at === index ? { ...one, ...patch } : one)));
  }

  function setUnit(index: number, written: string) {
    const unit = written.trim() || null;

    if (unit) {
      units.remember(unit);
    }

    replace(index, { quantity: { ...ingredients[index]!.quantity, unit } });
  }

  function setAmount(index: number, written: string) {
    const value = Number(written.replace(',', '.'));

    replace(index, {
      quantity: {
        ...ingredients[index]!.quantity,
        value: written.trim() && Number.isFinite(value) && value > 0 ? value : null
      }
    });
  }

  function remove(index: number) {
    editing = null;
    onchange(ingredients.filter((_, candidate) => candidate !== index));
  }
</script>

<div class="editor">
  <ul class="list">
    {#each ingredients as ingredient, index (index)}
      <li class="row" class:open={editing === index}>
        {#if editing === index}
          <div class="parts">
            <label class="part amount-field">
              <span>{m['editor.amount']()}</span>
              <input
                class="ds-control"
                type="text"
                inputmode="decimal"
                value={ingredient.quantity.value === null ? '' : String(ingredient.quantity.value)}
                oninput={(event) => setAmount(index, event.currentTarget.value)}
              />
            </label>

            <label class="part unit-field">
              <span>{m['editor.unit']()}</span>
              <!-- A list you can also type into. Native, so the keyboard, the
                   screen reader and the phone's own suggestions all work, and
                   typing something that is not on the list is allowed rather
                   than merely tolerated — that is how a unit gets added. -->
              <input
                class="ds-control"
                type="text"
                list="known-units"
                autocomplete="off"
                value={ingredient.quantity.unit ?? ''}
                oninput={(event) => setUnit(index, event.currentTarget.value)}
              />
            </label>

            <label class="part name-field">
              <span>{m['editor.ingredientName']()}</span>
              <input
                class="ds-control"
                type="text"
                value={ingredient.name}
                oninput={(event) => replace(index, { name: event.currentTarget.value })}
              />
            </label>

            <label class="part note-field">
              <span>{m['editor.ingredientNote']()}</span>
              <input
                class="ds-control"
                type="text"
                value={ingredient.note ?? ''}
                oninput={(event) =>
                  replace(index, { note: event.currentTarget.value.trim() || null })}
              />
            </label>
          </div>
        {:else}
          <span class="amount">{shown(ingredient)}</span>

          <span class="name">
            {ingredient.name}{#if ingredient.note}<span class="note">, {ingredient.note}</span>{/if}
          </span>
        {/if}

        <div class="controls">
          <IconButton
            label={editing === index
              ? m['editor.doneWithIngredient']({ name: ingredient.name })
              : m['editor.editIngredient']({ name: ingredient.name })}
            size="sm"
            onclick={() => (editing = editing === index ? null : index)}
          >
            {#if editing === index}
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
            onclick={() => remove(index)}
          >
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="m6 6 12 12M18 6 6 18" stroke-linecap="round" />
            </svg>
          </IconButton>
        </div>
      </li>
    {/each}
  </ul>

  <!-- Enter adds the line and puts the cursor back, so a whole list can be
       typed without ever reaching for the mouse. -->
  <div class="line">
    <input
      bind:this={input}
      class="ds-control"
      id="ingredient-line"
      type="text"
      autocomplete="off"
      bind:value={line}
      aria-label={m['editor.ingredientLine']()}
      placeholder={m['editor.ingredientHint']()}
      role="combobox"
      aria-expanded={open}
      aria-controls={listId}
      aria-autocomplete="list"
      aria-activedescendant={open ? `${listId}-${highlighted}` : undefined}
      oninput={() => (highlighted = 0)}
      onblur={() => (dismissed = line)}
      {onkeydown}
    />

    {#if open}
      <SuggestionList
        id={listId}
        label={m['editor.ingredientListLabel']()}
        items={options}
        {highlighted}
        onchoose={choose}
      />
    {/if}
  </div>

  <!-- One list for every row: the options are the same, and thirteen copies of
       it in the DOM would be thirteen copies to keep in step. -->
  <datalist id="known-units">
    {#each units.all as unit (unit)}<option value={unit}></option>{/each}
  </datalist>
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

  .line {
    position: relative;
  }

  .row {
    display: grid;
    grid-template-columns: minmax(4rem, auto) 1fr auto;
    align-items: center;
    gap: var(--space-3);
    padding-block: var(--space-1);
  }

  .row.open {
    grid-template-columns: 1fr auto;
    align-items: end;
    padding-block: var(--space-3);
  }

  .parts {
    display: grid;
    grid-template-columns: 5rem 7rem 1fr 1fr;
    gap: var(--space-2);
  }

  .part {
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
    min-width: 0;
    color: var(--text-subtle);
    font-size: var(--text-xs);
  }

  .controls {
    display: flex;
    gap: var(--space-1);
  }

  .amount {
    font-variant-numeric: tabular-nums;
    font-weight: var(--weight-medium);
    white-space: nowrap;
  }

  .note {
    color: var(--text-muted);
  }

  /* Four fields do not fit a phone. They stack into two rows of two, which
     keeps the amount beside its unit — the pair that is read together. */
  @media (max-width: 33.999rem) {
    .parts {
      grid-template-columns: 5rem 1fr;
    }

    .name-field,
    .note-field {
      grid-column: 1 / -1;
    }
  }
</style>
