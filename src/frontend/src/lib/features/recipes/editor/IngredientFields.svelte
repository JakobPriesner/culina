<script lang="ts" module>
  import { unitFor, unitLabel } from '../quantityLabels';
  import type { Ingredient } from '../types';

  /**
   * An ingredient while being typed; the amount stays a string so half-typed values like "1,"
   * survive until the edit settles.
   */
  export interface IngredientDraft {
    readonly amount: string;
    readonly unit: string;
    readonly name: string;
    readonly note: string;
  }

  export const emptyDraft: IngredientDraft = { amount: '', unit: '', name: '', note: '' };

  export const draftOf = (ingredient: Ingredient): IngredientDraft => ({
    amount: ingredient.quantity.value === null ? '' : String(ingredient.quantity.value),
    unit: ingredient.quantity.unit ? unitLabel(ingredient.quantity.unit) : '',
    name: ingredient.name,
    note: ingredient.note ?? ''
  });

  const amountOf = (written: string): number | null => {
    const value = Number(written.replace(',', '.'));

    return written.trim() && Number.isFinite(value) && value > 0 ? value : null;
  };

  export const toIngredient = (draft: IngredientDraft, id: string): Ingredient => ({
    id,
    quantity: { value: amountOf(draft.amount), unit: unitFor(draft.unit) || null },
    name: draft.name.trim(),
    note: draft.note.trim() || null
  });
</script>

<script lang="ts">
  import ComboField from './ComboField.svelte';
  import type { Suggestion } from './SuggestionList.svelte';
  import { m } from '$shell/i18n';
  import { nameOf, type Section } from '$features/shopping/sections';
  import { ingredients as known } from '../stores/ingredients.svelte';
  import { units } from '../stores/units.svelte';

  /**
   * One ingredient's fields, shared by adding, correcting and the shopping list; unit and name
   * lists are open vocabularies.
   */
  interface Props {
    id: string;
    /**
     * Names the group for screen readers, since an open correction row repeats "Amount" and "Unit".
     */
    label: string;
    value: IngredientDraft;
    onchange: (draft: IngredientDraft) => void;
    nameLabel?: string;
    note?: boolean;
    onsubmit?: () => void;
    householdId: string;
    /** The recipe's language, not the reader's, so suggestions match the words in the list. */
    language: string;
  }

  let {
    id,
    label,
    value,
    onchange,
    nameLabel,
    note = true,
    onsubmit,
    householdId,
    language
  }: Props = $props();

  let naming = $state(false);

  const typedUnit = $derived(value.unit.trim().toLowerCase());

  /**
   * The units on offer, narrowed by what was typed; a unit already written out in full is not
   * offered back.
   */
  const unitOptions = $derived<readonly Suggestion[]>(
    units.all
      .map((one) => unitLabel(one))
      .filter((word) => {
        const folded = word.toLowerCase();

        return folded !== typedUnit && (!typedUnit || folded.includes(typedUnit));
      })
      // The word is the value as well as the label; `toIngredient` turns it back into a unit.
      .map((word) => ({ value: word, label: word }))
  );

  const nameOptions = $derived<readonly Suggestion[]>(
    known.items
      .filter((one) => one.name.toLowerCase() !== value.name.trim().toLowerCase())
      .map((one) => ({
        value: one.name,
        label: one.name,
        detail: one.own ? undefined : nameOf(one.section as Section)
      }))
  );

  // Asked for on a pause, not per keystroke (answers come back out of order), and only while this
  // field has the cursor since the store is shared.
  $effect(() => {
    if (!naming) {
      return;
    }

    const wanted = value.name.trim();

    if (!wanted) {
      known.clear();

      return;
    }

    const timer = setTimeout(() => void known.suggest(householdId, wanted, language), 180);

    return () => clearTimeout(timer);
  });
</script>

<div class="field-container">
  <div class="fields" class:plain={!note} role="group" aria-label={label}>
    <label class="field amount">
      <span class="label">{m['editor.amount']()}</span>
      <input
        id="{id}-amount"
        class="ds-control"
        type="text"
        inputmode="decimal"
        autocomplete="off"
        value={value.amount}
        oninput={(event) => onchange({ ...value, amount: event.currentTarget.value })}
        onkeydown={(event) => {
          if (event.key === 'Enter') {
            event.preventDefault();
            onsubmit?.();
          }
        }}
      />
    </label>

    <div class="unit">
      <ComboField
        id="{id}-unit"
        label={m['editor.unit']()}
        listLabel={m['editor.unitListLabel']()}
        value={value.unit}
        options={unitOptions}
        oninput={(unit) => onchange({ ...value, unit })}
        {onsubmit}
      />
    </div>

    <div class="name">
      <ComboField
        id="{id}-name"
        label={nameLabel ?? m['editor.ingredientName']()}
        listLabel={m['editor.ingredientListLabel']()}
        value={value.name}
        options={nameOptions}
        oninput={(name) => onchange({ ...value, name })}
        bind:focused={naming}
        {onsubmit}
      />
    </div>

    {#if note}
      <label class="field note">
        <span class="label">{m['editor.ingredientNote']()}</span>
        <input
          id="{id}-note"
          class="ds-control"
          type="text"
          autocomplete="off"
          value={value.note}
          oninput={(event) => onchange({ ...value, note: event.currentTarget.value })}
          onkeydown={(event) => {
            if (event.key === 'Enter') {
              event.preventDefault();
              onsubmit?.();
            }
          }}
        />
      </label>
    {/if}
  </div>
</div>

<style>
  .field-container {
    container: ingredient-fields / inline-size;
    min-width: 0;
  }

  .fields {
    display: grid;
    grid-template-columns: 5rem minmax(0, 1fr);
    gap: var(--space-2);
    align-items: end;
  }

  .name,
  .note {
    grid-column: 1 / -1;
  }

  .field {
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
    min-width: 0;
  }

  .label {
    color: var(--text-subtle);
    font-size: var(--text-xs);
  }

  .unit,
  .name {
    min-width: 0;
  }

  /* Layout follows the container's width, not the device's. */
  @container ingredient-fields (min-width: 28rem) {
    .plain {
      grid-template-columns: 5rem 8rem minmax(0, 1fr);
    }

    .plain .name {
      grid-column: auto;
    }
  }

  @container ingredient-fields (min-width: 36rem) {
    .fields:not(.plain) {
      grid-template-columns: 5rem 8rem minmax(0, 1fr) minmax(0, 1fr);
    }

    .name,
    .note {
      grid-column: auto;
    }
  }
</style>
