<script lang="ts">
  import { tick } from 'svelte';

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
   * The ingredient list, one ingredient at a time, with each ingredient's steps shown read-only
   * underneath.
   * Ingredients are attached to steps under the steps, so there is one place to do that.
   */
  interface Props {
    ingredients: readonly Ingredient[];
    steps: readonly Step[];
    onchange: (ingredients: Ingredient[]) => void;
    householdId: string;
    language: string;
    /** An ingredient to open and put the cursor in, when the page was reached from a link to it. */
    focusId?: string | null;
  }

  let { ingredients, steps, onchange, householdId, language, focusId = null }: Props = $props();

  const usage = $derived(usageOf(steps));

  let adding = $state<IngredientDraft>(emptyDraft);
  let editing = $state<number | null>(null);
  let editingDraft = $state<IngredientDraft>(emptyDraft);

  // Once per link: opening it must not pull the cursor back after the reader has moved on.
  let focused: string | null = null;

  $effect(() => {
    const index = focusId ? ingredients.findIndex((one) => one.id === focusId) : -1;

    if (index < 0 || focused === focusId) {
      return;
    }

    focused = focusId;
    editing = index;
    editingDraft = draftOf(ingredients[index]!);

    void tick().then(() => {
      const field = document.getElementById(`ingredient-${index}-name`);

      field?.scrollIntoView({ block: 'center' });
      field?.focus({ preventScroll: true });
    });
  });

  /**
   * Keeps a unit once it has settled, not per keystroke, or typing "Schuss" would save S, Sc,
   * Sch...
   */
  function remember(draft: IngredientDraft) {
    // The field holds a word, not a unit: read it as one first, or "Zehe" would be filed as a new
    // unit.
    const unit = unitFor(draft.unit);

    if (unit) {
      units.remember(unit);
    }
  }

  function add() {
    if (!adding.name.trim()) {
      return;
    }

    remember(adding);

    // No id: the server assigns one.
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
   * Writes a correction straight through, per keystroke, so autosave sees it like any other edit.
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
      <p class="none">{m['editor.ingredientsEmpty']()}</p>
    {/if}

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
   * A hairline border, never a shadow, so the list and the add row read as one block next to the
   * method.
   */
  .panel {
    min-width: 0;
    border: 1px solid var(--border);
    border-radius: var(--radius-lg);
    background: var(--surface-raised);
  }

  /*
   * One grid for the whole list, so names align instead of following each row's own amount column.
   */
  .list {
    display: grid;
    grid-template-columns: minmax(5rem, max-content) minmax(0, 1fr) auto;
    column-gap: var(--space-4);
    margin: 0;
    padding: 0;
    list-style: none;
  }

  /* The line between rows belongs to the list; a row doesn't know whether it has a neighbour. */
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

  /*
   * On a phone the fields already stack, so the buttons go underneath (also the corrected row, to
   * keep its width).
   */
  @container ingredient-editor (width < 44rem) {
    .add {
      grid-template-columns: 1fr;
    }

    .add :global(.button) {
      justify-self: start;
    }
  }
</style>
