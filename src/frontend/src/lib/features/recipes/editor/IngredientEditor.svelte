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
  <div class="panel" class:filled={ingredients.length > 0}>
    {#if ingredients.length > 0}
      <ul class="list">
        {#each ingredients as ingredient, index (index)}
          <li class="row" class:open={editing === index}>
            {#if editing === index}
              <div class="fields">
                <IngredientFields
                  id="ingredient-{index}"
                  label={m['editor.editIngredient']({ name: ingredient.name })}
                  value={editingDraft}
                  onchange={correct}
                  onsubmit={close}
                  {householdId}
                  {language}
                />
              </div>
            {:else}
              <span class="amount">{shown(ingredient)}</span>

              <div class="name">
                <span class="written"
                  >{ingredient.name}{#if ingredient.note}<span class="note"
                      >, {ingredient.note}</span
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
    {:else}
      <!-- Said once, where the first line will go. An empty list with nothing
           but four blank fields under it is a form; this is a recipe that has
           not been shopped for yet. -->
      <p class="none">{m['editor.ingredientsEmpty']()}</p>
    {/if}

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
  </div>

  <p class="hint">{m['editor.ingredientHint']()}</p>
</div>

<style>
  .editor {
    container: ingredient-editor / inline-size;
    min-width: 0;
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
  }

  /*
   * A hairline enclosure, not a card.
   *
   * The list and the line being added to it are one thing, and nothing else on
   * the page says so: a run of rows and a row of fields a gap apart look
   * exactly like two unrelated blocks. The same enclosure the settings screens
   * use, for the same reason and with the same restraint — a border, never a
   * shadow, because a shadow would lift the ingredients off the page as if they
   * were a separate document from the method below them.
   */
  .panel {
    min-width: 0;
    border: 1px solid var(--border);
    border-radius: var(--radius-lg);
    background: var(--surface-raised);
  }

  /*
   * One grid for the whole list, not one per row.
   *
   * A row that sizes its own amount column leaves every name starting somewhere
   * different — "1 Päckchen Vanillezucker" pushes its name three characters
   * past "125 g Butter" — and the list reads as a ragged pile rather than a
   * written-out recipe. The same arrangement, and the same reasoning, as the
   * list on the page that reads the recipe back.
   */
  .list {
    display: grid;
    grid-template-columns: minmax(5rem, max-content) minmax(0, 1fr) auto;
    column-gap: var(--space-4);
    margin: 0;
    padding: 0;
    list-style: none;
  }

  .row {
    display: grid;
    grid-column: 1 / -1;
    grid-template-columns: subgrid;
    align-items: center;
    padding: var(--space-2) var(--space-4);
    transition: background-color var(--duration-fast) var(--ease-out);
  }

  /* The line between two rows belongs to the list rather than to a row: whether
     a row has a neighbour is not something the row knows. */
  .row + .row,
  .panel.filled .add {
    border-top: 1px solid var(--border);
  }

  .row:hover {
    background: var(--surface-hover);
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

  /* The row being corrected keeps its place in the list and is marked rather
     than moved: the fields open where the line was, so the eye does not have to
     find it again. */
  .row.open {
    grid-template-columns: minmax(0, 1fr) auto;
    align-items: end;
    padding-block: var(--space-4);
    background: var(--surface-sunken);
  }

  .row.open .fields {
    min-width: 0;
  }

  .add {
    display: grid;
    grid-template-columns: minmax(0, 1fr) auto;
    align-items: end;
    gap: var(--space-3);
    padding: var(--space-4);
  }

  .none {
    padding: var(--space-4);
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .controls {
    display: flex;
    gap: var(--space-1);
  }

  /*
   * The row's own controls, which are only the row's business while you are on
   * it.
   *
   * Eight ingredients means sixteen icon buttons down the right-hand edge, and
   * a list that reads as a toolbar rather than as a recipe. They fade in for the
   * pointer that is on the row and for the keyboard that has reached it — and
   * on a touch screen, where there is no hovering and nothing to reveal them,
   * they simply stay.
   */
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

  .hint {
    margin: 0;
    padding-inline: var(--space-1);
    color: var(--text-subtle);
    font-size: var(--text-xs);
  }

  /* Keep the written name readable when quantity and actions would consume
     the row, including a narrow editor with enlarged text. */
  @container ingredient-editor (width < 24rem) {
    .list {
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

    /* Sized to its own words rather than stretched across the panel: the
       button is disabled until there is a name to add, and a full-width grey
       slab is the heaviest thing that can be said with a control nobody can
       press yet. */
    .add :global(.button) {
      justify-self: start;
    }

    .row.open .controls {
      justify-content: flex-end;
    }
  }
</style>
