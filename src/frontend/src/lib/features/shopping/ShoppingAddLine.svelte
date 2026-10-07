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

  /** Adds one line using the recipe editor's ingredient fields, so household units and known names are offered too. */
  async function add() {
    const line = toIngredient(typed, '');

    // The name is the item; an amount alone is dropped.
    if (!line.name || !householdId) {
      return;
    }

    // Remembered on submit, not per keystroke, so partial typing ("S", "Sc") doesn't become a unit.
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

<div class="adding">
  <!-- Enter adds the line and refocuses the amount, so a list can be typed without the mouse. -->
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

  /* Fields already stack on a phone; the button goes underneath. */
  @container shopping-entry (width < 40rem) {
    .add {
      grid-template-columns: 1fr;
    }
  }
</style>
