<script lang="ts" module>
  import { unitFor, unitLabel } from '../quantityLabels';
  import type { Ingredient } from '../types';

  /**
   * An ingredient while it is being typed, as text.
   *
   * The amount is a string here and a number in the model, and that difference
   * is the point: "1," and "0." and "" are all things a half-typed amount looks
   * like, and none of them survive a trip through `Number`. Keeping the typed
   * text until the edit settles means the field shows what was typed into it.
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

  /** Reads a typed amount, which is null until it is a positive number. */
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
   * One ingredient, as the things it is made of.
   *
   * The same fields whether the ingredient is being added or corrected,
   * because they are the same fields — one component so a change to how an
   * amount is read cannot apply to only half of the editor. The shopping list
   * writes a line the same way, which is why the preparation field is
   * optional rather than assumed.
   *
   * Both lists are open vocabularies. The thirteen built-in units are offered,
   * this kitchen's own follow them, and a unit nobody has written before is
   * added by writing it; ingredient names come from the household's own recipes
   * first and a short seeded list after. Neither has to be chosen from.
   */
  interface Props {
    /** Prefixes the field ids, so two of these on one screen stay distinct. */
    id: string;
    /**
     * What this row of fields is, for a screen reader.
     *
     * An open correction row puts a second "Amount" and a second "Unit" on the
     * screen, and out of context the two are indistinguishable. Naming the
     * group is what tells somebody listening which ingredient they are in.
     */
    label: string;
    value: IngredientDraft;
    onchange: (draft: IngredientDraft) => void;
    /**
     * What the third field is called.
     *
     * A shopping list holds washing-up liquid as readily as it holds shallots,
     * and calling that an ingredient is calling it the wrong thing.
     */
    nameLabel?: string;
    /**
     * Whether to ask how it is prepared.
     *
     * Off where there is nothing to prepare: "finely chopped" belongs to a
     * recipe, and a shopping line has nowhere to keep it.
     */
    note?: boolean;
    /** Enter in any field, when no suggestion is highlighted. */
    onsubmit?: () => void;
    /** Whose kitchen, so the names offered are this household's own words. */
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
   * The units on offer, narrowed by what has been typed so far.
   *
   * A unit already written out in full is dropped rather than offered back —
   * the list is there to save typing, and its only row being the word already
   * on screen saves none.
   */
  const unitOptions = $derived<readonly Suggestion[]>(
    units.all
      .map((one) => unitLabel(one))
      .filter((word) => {
        const folded = word.toLowerCase();

        return folded !== typedUnit && (!typedUnit || folded.includes(typedUnit));
      })
      // The word is the value as well as the label: this field holds words,
      // and `toIngredient` is what turns the chosen word back into a unit.
      .map((word) => ({ value: word, label: word }))
  );

  const nameOptions = $derived<readonly Suggestion[]>(
    known.items
      .filter((one) => one.name.toLowerCase() !== value.name.trim().toLowerCase())
      .map((one) => ({
        value: one.name,
        label: one.name,
        // Where it lives in a shop, which is the one thing about an ingredient
        // that is useful to know before you have finished typing its name.
        detail: one.own ? undefined : nameOf(one.section as Section)
      }))
  );

  // Asked for on a pause rather than on a keystroke: a request per letter is a
  // request per letter, and the answers come back out of order anyway. Only
  // while the cursor is in this field, because the store that answers is shared
  // and the field somebody is typing in owns the question.
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

  /* The same fields appear in narrow shopping pages and wider editors.
     Their own available width, not the device width, determines the layout. */
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
