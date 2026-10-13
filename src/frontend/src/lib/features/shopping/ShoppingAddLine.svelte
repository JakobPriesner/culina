<script lang="ts">
  import { Button } from '$ds';
  import {
    emptyDraft,
    fromLine,
    toIngredient
  } from '$features/recipes/editor/IngredientFields.svelte';
  import ComboField from '$features/recipes/editor/ComboField.svelte';
  import { ingredients as known } from '$features/recipes/stores/ingredients.svelte';
  import { unitFor } from '$features/recipes/quantityLabels';
  import { units } from '$features/recipes/stores/units.svelte';
  import { m } from '$shell/i18n';
  import { preferences } from '$shell/preferences.svelte';
  import { nameOf, type Section } from './sections';
  import { shopping } from './stores/shopping.svelte';

  interface Props {
    householdId: string | null;
    onaddrecipe: () => void;
  }
  let { householdId, onaddrecipe }: Props = $props();
  let typed = $state('');
  let focused = $state(false);
  let asked = $state('');
  const draft = $derived(fromLine({ ...emptyDraft, name: typed }, units.own));
  const wanted = $derived(draft.name.trim());
  const answered = $derived(Boolean(wanted) && asked === wanted && known.answered === wanted);
  // Keep a written quantity when a suggested ingredient name is selected.
  const options = $derived(
    answered
      ? known.items
          .filter((item) => item.name.toLowerCase() !== wanted.toLowerCase())
          .map((item) => ({
            value: [draft.amount, draft.unit, item.name].filter(Boolean).join(' '),
            label: item.name,
            detail: item.own ? undefined : nameOf(item.section as Section)
          }))
      : []
  );

  $effect(() => {
    if (!focused || !householdId || !wanted) return;
    const id = householdId;
    const query = wanted;
    const language = preferences.locale;
    const timer = setTimeout(() => {
      asked = query;
      void known.suggest(id, query, language);
    }, 180);
    return () => clearTimeout(timer);
  });

  async function add() {
    const line = toIngredient(draft, '');
    if (!line.name || !householdId) return;
    const unit = unitFor(draft.unit);
    if (unit) units.remember(unit);
    typed = '';
    document.getElementById('shopping-add-name')?.focus();
    await shopping.add(
      householdId,
      line.name,
      line.quantity.value ?? undefined,
      line.quantity.unit
    );
  }
</script>

<div class="adding">
  <div class="add" role="group" aria-label={m['shopping.addFields']()}>
    <ComboField
      id="shopping-add-name"
      label={m['shopping.itemName']()}
      showLabel={false}
      placeholder={m['shopping.quickAdd.placeholder']()}
      listLabel={m['editor.ingredientListLabel']()}
      value={typed}
      {options}
      oninput={(value) => (typed = value)}
      onsubmit={add}
      bind:focused
    />
    <Button
      label={m['shopping.add']()}
      onclick={add}
      disabled={!typed.trim() || !householdId}
      variant="primary"
      size="sm"
    >
      <span class="plus" aria-hidden="true">+</span>
    </Button>
  </div>
  <Button variant="ghost" size="sm" onclick={onaddrecipe}>
    {#snippet icon()}
      <svg
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        stroke-width="1.8"
        stroke-linecap="round"
        stroke-linejoin="round"
      >
        <path d="M12 5C9 3 5 3 3 4v15c3-1 6-1 9 1 3-2 6-2 9-1V4c-2-1-6-1-9 1v15" />
      </svg>
    {/snippet}
    {m['shopping.addRecipe']()}
  </Button>
</div>

<style>
  .plus {
    display: block;
    width: var(--space-6);
    font-size: var(--text-lg);
    line-height: 1;
  }
  .adding {
    display: flex;
    flex-direction: column;
    align-items: flex-start;
    gap: var(--space-2);
    margin-bottom: var(--space-6);
  }
  .add {
    display: grid;
    grid-template-columns: minmax(0, 1fr) auto;
    align-items: center;
    gap: var(--space-2);
    width: 100%;
  }
</style>
