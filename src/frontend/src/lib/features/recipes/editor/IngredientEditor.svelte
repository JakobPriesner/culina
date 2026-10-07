<script lang="ts">
  import { Button } from '$ds';

  import { m } from '$shell/i18n';
  import IngredientFields, {
    draftOf,
    emptyDraft,
    toIngredient,
    type IngredientDraft
  } from './IngredientFields.svelte';
  import { unitFor } from '../quantityLabels';
  import { units } from '../stores/units.svelte';
  import IngredientRow from './IngredientRow.svelte';
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

  let adding = $state<IngredientDraft>(emptyDraft);
  /** Which row is open for correction, by position. Only ever one. */
  let editing = $state<number | null>(null);
  let editingDraft = $state<IngredientDraft>(emptyDraft);

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
          <IngredientRow
            {ingredient}
            {index}
            open={editing === index}
            draft={editingDraft}
            usedIn={ingredient.id ? (usage.get(ingredient.id) ?? []) : []}
            {householdId}
            {language}
            oncorrect={correct}
            onclose={close}
            ontoggle={() => open(index)}
            onremove={() => remove(index)}
          />
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

  /* The line between two rows belongs to the list rather than to a row: whether
     a row has a neighbour is not something the row knows. */
  .list > :global(.row + .row),
  .panel.filled .add {
    border-top: 1px solid var(--border);
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

  .hint {
    margin: 0;
    padding-inline: var(--space-1);
    color: var(--text-subtle);
    font-size: var(--text-xs);
  }

  @container ingredient-editor (width < 24rem) {
    .list {
      grid-template-columns: minmax(0, 1fr) auto;
    }
  }

  /* On a phone the fields already stack, and a control beside them would have
     nothing but a sliver left. They go underneath instead — and the row being
     corrected does the same, or its four fields would be squeezed to make room
     for two icons while the row below them used the whole width. */
  @container ingredient-editor (width < 44rem) {
    .add {
      grid-template-columns: 1fr;
    }

    /* Sized to its own words rather than stretched across the panel: the
       button is disabled until there is a name to add, and a full-width grey
       slab is the heaviest thing that can be said with a control nobody can
       press yet. */
    .add :global(.button) {
      justify-self: start;
    }
  }
</style>
