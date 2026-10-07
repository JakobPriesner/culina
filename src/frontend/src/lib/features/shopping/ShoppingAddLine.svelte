<script lang="ts">
  import { Button } from '$ds';
  import IngredientFields, {
    emptyDraft,
    toIngredient,
    type IngredientDraft
  } from '$features/recipes/editor/IngredientFields.svelte';
  import { unitFor } from '$features/recipes/quantityLabels';
  import { units } from '$features/recipes/stores/units.svelte';
  import { m } from '$shell/i18n';
  import { preferences } from '$shell/preferences.svelte';
  import { shopping } from './stores/shopping.svelte';

  interface Props {
    householdId: string | null;
  }

  let { householdId }: Props = $props();

  let typed = $state<IngredientDraft>(emptyDraft);

  /**
   * One line in, written the way an ingredient is written into a recipe.
   *
   * The same three fields as the editor, and the same component: an amount, a
   * unit and a name are three things, and a single box that has to be taken
   * apart afterwards guesses at where each one ends. It also means the unit
   * this household invented and the names already in its recipes are offered
   * here too — a shopping list is mostly words it has seen before.
   */
  async function add() {
    const line = toIngredient(typed, '');

    // The name is the item. An amount with nothing to measure is not a
    // half-finished line worth keeping, it is a line that says nothing.
    if (!line.name || !householdId) {
      return;
    }

    // Kept on settling rather than per keystroke, so writing "Schuss" does not
    // leave S, Sc and Sch behind as units this kitchen measures in.
    const unit = unitFor(typed.unit);

    if (unit) {
      units.remember(unit);
    }

    typed = emptyDraft;
    document.getElementById('shopping-add-amount')?.focus();

    await shopping.add(
      householdId,
      line.name,
      line.quantity.value ?? undefined,
      line.quantity.unit
    );
  }
</script>

<!--
  A panel, not a band between two rules.

  Three fields and a button with a hairline above and below them is the shape
  of a form somebody has been sent to fill in. The same material the
  ingredient list is drawn on says the other thing: this belongs to the list
  under it, and it is where lines come from.
-->
<div class="adding">
  <!-- Enter adds the line and puts the cursor back on the amount, so a whole
       list can be written without ever reaching for the mouse. -->
  <div class="add">
    <IngredientFields
      id="shopping-add"
      label={m['shopping.addFields']()}
      nameLabel={m['shopping.itemName']()}
      note={false}
      value={typed}
      onchange={(draft) => (typed = draft)}
      onsubmit={add}
      householdId={householdId ?? ''}
      language={preferences.locale}
    />

    <Button variant="primary" onclick={add} disabled={!typed.name.trim()}>
      {m['shopping.add']()}
    </Button>
  </div>

  <p class="hint">{m['shopping.addHint']()}</p>
</div>

<style>
  .adding {
    container: shopping-entry / inline-size;
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
    padding: var(--space-4);
    border-radius: var(--radius-lg);
    background: var(--surface-sunken);
    margin-bottom: var(--space-8);
  }

  .add {
    display: grid;
    grid-template-columns: minmax(0, 1fr) auto;
    align-items: end;
    gap: var(--space-3);
  }

  .hint {
    color: var(--text-muted);
    font-size: var(--text-xs);
  }

  /* On a phone the fields already stack, and a button beside them would have
     nothing but a sliver left. It goes underneath instead. */
  @container shopping-entry (width < 40rem) {
    .add {
      grid-template-columns: 1fr;
    }
  }
</style>
