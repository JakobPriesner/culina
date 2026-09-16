<script lang="ts">
  import { Button, IconButton } from '$ds';

  import { m } from '$shell/i18n';
  import IngredientFields, {
    draftOf,
    emptyDraft,
    toIngredient,
    type IngredientDraft
  } from './IngredientFields.svelte';
  import { formatQuantity } from '../formatQuantity';
  import { quantityLabels, unitFor } from '../quantityLabels';
  import { scaleQuantity } from '../scaling';
  import { units } from '../stores/units.svelte';
  import { preferences } from '$shell/preferences.svelte';
  import { usageOf } from './stepUsage';
  import type { Ingredient, Step } from '../types';

  /**
   * The ingredient list, written one ingredient at a time.
   *
   * An amount, a unit and a name, each in its own field, because each is its
   * own thing: the amount scales, the unit converts, and the name is what ends
   * up on a shopping list. Typing them apart is what makes them separable
   * without a parser having to guess where one ends and the next begins.
   *
   * A written ingredient reads back as one line — "200 g flour, sifted" — since
   * that is how a recipe reads. The fields only reappear to correct it, and
   * they are the same fields it was written in.
   *
   * Under each line is where it ends up in the method. Read-only on purpose:
   * ingredients are put on steps under the steps, and one thing that can be
   * done in two places is how the two places start disagreeing. "Not in a step"
   * is said quietly rather than flagged, because salt to taste belongs to no
   * step and never will.
   */
  interface Props {
    ingredients: readonly Ingredient[];
    /** The method, read backwards: which steps each ingredient ends up in. */
    steps: readonly Step[];
    onchange: (ingredients: Ingredient[]) => void;
    /** Whose kitchen, so the suggestions are this household's own words. */
    householdId: string;
    /** What the recipe is written in, which is what its names are in. */
    language: string;
  }

  let { ingredients, steps, onchange, householdId, language }: Props = $props();

  const usage = $derived(usageOf(steps));

  /** Focuses the step itself, which is where anything about it is changed. */
  const goTo = (number: number) => document.getElementById(`step-${number - 1}`)?.focus();

  let adding = $state<IngredientDraft>(emptyDraft);
  /** Which row is open for correction, by position. Only ever one. */
  let editing = $state<number | null>(null);
  let editingDraft = $state<IngredientDraft>(emptyDraft);

  const shown = (ingredient: Ingredient) =>
    formatQuantity(scaleQuantity(ingredient.quantity, 1), preferences.locale, quantityLabels).text;

  /**
   * Keeps a unit somebody wrote, once they have finished writing it.
   *
   * On settling rather than on every keystroke, or typing "Schuss" would leave
   * behind S, Sc, Sch and every other prefix of it as units this kitchen
   * measures in.
   */
  function remember(draft: IngredientDraft) {
    // The field holds a word and the store holds units, so the word has to be
    // read as one first — otherwise choosing "Zehe" would file the German for
    // `clove` as a unit this kitchen invented.
    const unit = unitFor(draft.unit);

    if (unit) {
      units.remember(unit);
    }
  }

  function add() {
    // The name is the ingredient. An amount with nothing to measure is not a
    // half-finished row worth keeping, it is a row that says nothing.
    if (!adding.name.trim()) {
      return;
    }

    remember(adding);

    // No id: the server assigns one, and an ingredient that has never been
    // saved has no identity to borrow.
    onchange([...ingredients, toIngredient(adding, '')]);

    adding = emptyDraft;
    document.getElementById('add-ingredient-amount')?.focus();
  }

  function open(index: number) {
    if (editing === index) {
      close();

      return;
    }

    editing = index;
    editingDraft = draftOf(ingredients[index]!);
  }

  function close() {
    remember(editingDraft);
    editing = null;
  }

  /**
   * Writes a correction straight through to the recipe.
   *
   * Per keystroke, so the autosave that watches the recipe sees the edit the
   * same way it sees every other one — there is no separate moment where a
   * correction is committed and nothing to lose by navigating away.
   */
  function correct(draft: IngredientDraft) {
    editingDraft = draft;

    const index = editing;

    if (index === null) {
      return;
    }

    onchange(ingredients.map((one, at) => (at === index ? toIngredient(draft, one.id) : one)));
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
          <IngredientFields
            id="ingredient-{index}"
            label={m['editor.editIngredient']({ name: ingredient.name })}
            value={editingDraft}
            onchange={correct}
            onsubmit={close}
            {householdId}
            {language}
          />
        {:else}
          <span class="amount">{shown(ingredient)}</span>

          <div class="name">
            <span class="written"
              >{ingredient.name}{#if ingredient.note}<span class="note">, {ingredient.note}</span
                >{/if}</span
            >

            {#if ingredient.id}
              {@const inSteps = usage.get(ingredient.id) ?? []}

              <span class="where">
                {#if inSteps.length > 0}
                  <!-- A preposition rather than "Step", so the line is
                       grammatical whether there is one number or four. -->
                  {m['editor.usedInSteps']()}
                  {#each inSteps as number, at (number)}
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
            label={editing === index
              ? m['editor.doneWithIngredient']({ name: ingredient.name })
              : m['editor.editIngredient']({ name: ingredient.name })}
            size="sm"
            onclick={() => open(index)}
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

  <!-- Enter adds the ingredient and puts the cursor back on the amount, so a
       whole list can be typed without ever reaching for the mouse. -->
  <div class="add">
    <IngredientFields
      id="add-ingredient"
      label={m['editor.newIngredient']()}
      value={adding}
      onchange={(draft) => (adding = draft)}
      onsubmit={add}
      {householdId}
      {language}
    />

    <Button variant="secondary" size="sm" onclick={add} disabled={!adding.name.trim()}>
      {m['editor.addIngredient']()}
    </Button>
  </div>

  <p class="hint">{m['editor.ingredientHint']()}</p>
</div>

<style>
  .editor {
    container: ingredient-editor / inline-size;
    min-width: 0;
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
    grid-template-columns: minmax(4rem, auto) minmax(0, 1fr) auto;
    align-items: center;
    gap: var(--space-3);
    padding-block: var(--space-1);
  }

  .name {
    min-width: 0;
  }

  /* Where it ends up in the method. Muted, never a warning colour: an
     ingredient in no step is a normal recipe, not a mistake to fix. */
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
    padding-block: var(--space-3);
  }

  .add {
    display: grid;
    grid-template-columns: minmax(0, 1fr) auto;
    align-items: end;
    gap: var(--space-3);
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

  .hint {
    margin: 0;
    color: var(--text-muted);
    font-size: var(--text-xs);
  }

  /* Keep the written name readable when quantity and actions would consume
     the row, including a narrow editor with enlarged text. */
  @container ingredient-editor (width < 24rem) {
    .row:not(.open) {
      grid-template-columns: minmax(0, 1fr) auto;
    }

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

  /* On a phone the fields already stack, and a control beside them would have
     nothing but a sliver left. They go underneath instead — and the row being
     corrected does the same, or its four fields would be squeezed to make room
     for two icons while the row below them used the whole width. */
  @container ingredient-editor (width < 44rem) {
    .add,
    .row.open {
      grid-template-columns: 1fr;
    }

    .row.open .controls {
      justify-content: flex-end;
    }
  }
</style>
