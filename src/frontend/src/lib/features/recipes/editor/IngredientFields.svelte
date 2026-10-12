<script lang="ts" module>
  import { unitFor, unitLabel } from '../quantityLabels';
  import type { Ingredient } from '../types';
  import { parseIngredientLine } from './parseIngredientLine';

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

  /**
   * A whole line typed into the name field ("200 g Mehl, gesiebt") is read into its parts, as people
   * write it; only when amount and unit were left empty, so parts typed separately always win, and only
   * when the line starts with an amount, so "Salz, eine Prise" stays a name. `own` is the household's units.
   */
  export const fromLine = (
    draft: IngredientDraft,
    own: readonly string[] = []
  ): IngredientDraft => {
    if (draft.amount.trim() || draft.unit.trim()) {
      return draft;
    }

    const parsed = parseIngredientLine(draft.name, own);

    if (parsed.quantity.value === null) {
      return draft;
    }

    return {
      amount: String(parsed.quantity.value),
      unit: parsed.quantity.unit ? unitLabel(parsed.quantity.unit) : '',
      name: parsed.name,
      note: draft.note.trim() || parsed.note || ''
    };
  };

  export const toIngredient = (draft: IngredientDraft, id: string): Ingredient => ({
    id,
    quantity: { value: amountOf(draft.amount), unit: unitFor(draft.unit) || null },
    name: draft.name.trim(),
    note: draft.note.trim() || null
  });
</script>

<script lang="ts">
  import { Button } from '$ds';

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

  /** How well a unit matches what was typed: itself first, then units starting with it, then the rest. */
  const rank = (folded: string) =>
    folded === typedUnit ? 0 : folded.startsWith(typedUnit) ? 1 : 2;

  /**
   * The units on offer, narrowed by what was typed. A unit written out in full stays on offer, first:
   * leaving it out made "g" look invalid while "kg" and "Packung" were suggested.
   */
  const unitOptions = $derived<readonly Suggestion[]>(
    units.all
      .map((one) => unitLabel(one))
      .filter((word) => !typedUnit || word.toLowerCase().includes(typedUnit))
      .sort((a, b) => rank(a.toLowerCase()) - rank(b.toLowerCase()))
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

  /** What this field last asked suggestions for; the store is shared, so its answer may be another field's. */
  let asked = $state('');

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

    const timer = setTimeout(() => {
      asked = wanted;
      void known.suggest(householdId, wanted, language);
    }, 180);

    return () => clearTimeout(timer);
  });

  /** Whether the store's answer is to the name in the field; it stays after the field loses the cursor. */
  const answered = $derived(asked && asked === value.name.trim() && known.answered === asked);

  /**
   * The household's own name for what was typed, when it is spelt otherwise ("Hähnchenbrust" for
   * "Hühnchenbrustfilet"): offered so a near-duplicate is not saved.
   */
  const existing = $derived(
    answered
      ? (known.items.find((one) => one.equivalent && one.name !== asked)?.name ?? null)
      : null
  );

  const typed = $derived(answered ? known.typed : null);

  /** Preparation written into the name ("gehackte Zwiebeln"): offered for the note, never moved unasked. */
  const preparation = $derived(
    note && typed?.name && typed.note ? { name: typed.name, note: typed.note } : null
  );

  /** What a new name was understood as, when that is more than the name typed: "Kokosmilch, light". */
  const understood = $derived(
    typed &&
      typed.understood !== asked &&
      !known.items.some((one) => one.name.toLowerCase() === asked.toLowerCase())
      ? typed.understood
      : null
  );
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

  {#if preparation}
    <p class="hint" role="status">
      <span>{m['editor.ingredientPreparation']({ note: preparation.note })}</span>
      <Button
        size="sm"
        variant="ghost"
        label={m['editor.ingredientMoveToNoteLabel']({ note: preparation.note })}
        onclick={() =>
          onchange({
            ...value,
            name: preparation.name,
            note: [value.note.trim(), preparation.note].filter(Boolean).join(', ')
          })}
      >
        {m['editor.ingredientMoveToNote']()}
      </Button>
    </p>
  {:else if existing}
    <p class="hint" role="status">
      <span>{m['editor.ingredientExisting']({ name: existing })}</span>
      <Button
        size="sm"
        variant="ghost"
        label={m['editor.ingredientUseExistingLabel']({ name: existing })}
        onclick={() => onchange({ ...value, name: existing })}
      >
        {m['editor.ingredientUseExisting']()}
      </Button>
    </p>
  {:else if understood}
    <p class="hint" role="status">{m['editor.ingredientUnderstood']({ name: understood })}</p>
  {/if}
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

  .hint {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-1) var(--space-2);
    margin-top: var(--space-2);
    color: var(--text-muted);
    font-size: var(--text-sm);
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
