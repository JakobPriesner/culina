<script lang="ts">
  import { Button, Field, Select, Sheet, TextInput } from '$ds';

  import { m } from '$shell/i18n';
  import { preferences } from '$shell/preferences.svelte';
  import { formatQuantity } from '../formatQuantity';
  import { quantityLabels, unitLabel } from '../quantityLabels';
  import { scaleQuantity, targetYieldForAmount, yieldLabel } from '../scaling';
  import { scales } from '../units';
  import { wordYield } from '../yieldWords';
  import { parseIngredientLine } from '../editor/parseIngredientLine';
  import type { Quantity, RecipeReading } from '../types';

  /** "I have 600 g of flour": the same scaling machinery driven from the other end, typed as one line ("600 g") like the editor. */
  interface Props {
    open: boolean;
    recipe: RecipeReading;
    onapply: (targetYield: number) => void;
  }

  let { open = $bindable(), recipe, onapply }: Props = $props();

  const choices = $derived(
    recipe.groups
      .flatMap((group) => group.ingredients)
      // A pinch is not scaled, so nothing can be scaled from one either.
      .filter((one) => one.quantity.value !== null && scales(one.quantity.unit))
      .map((one) => ({ value: one.id, label: one.name }))
  );

  let chosen = $state('');
  let typed = $state('');

  const ingredient = $derived(
    recipe.groups
      .flatMap((group) => group.ingredients)
      .find((one) => one.id === (chosen || choices[0]?.value))
  );

  const written = (quantity: Quantity) =>
    formatQuantity(scaleQuantity(quantity, 1), preferences.locale, quantityLabels).text;

  /** The ingredient's own amount, as the example of what to type. */
  const example = $derived(ingredient ? written(ingredient.quantity) : '');

  /** What was typed as an amount of the chosen ingredient: a bare number takes its unit (said beside the field), a written unit is taken as written, and one needing density (ml for g) is refused out loud. */
  const available = $derived.by((): Quantity | null => {
    const trimmed = typed.trim();

    if (!ingredient || !trimmed) {
      return null;
    }

    const own = ingredient.quantity.unit ? [ingredient.quantity.unit] : [];
    const parsed = parseIngredientLine(trimmed, own);
    const bare = parsed.quantity.unit === null && parsed.name === trimmed;

    return bare ? { ...parsed.quantity, unit: ingredient.quantity.unit } : parsed.quantity;
  });

  const target = $derived(
    ingredient && available
      ? targetYieldForAmount(ingredient.quantity, available, recipe.yieldAmount)
      : null
  );

  /** Why there is no answer yet, once something has been typed. */
  const problem = $derived.by(() => {
    if (!ingredient || !available || target !== null) {
      return undefined;
    }

    return available.value && ingredient.quantity.unit
      ? m['scaleTo.otherUnit']({ unit: unitLabel(ingredient.quantity.unit) })
      : m['scaleTo.needsAmount']({ example });
  });

  const reading = $derived(
    available && target !== null ? m['scaleTo.readAs']({ amount: written(available) }) : undefined
  );

  const resultText = $derived(
    target === null
      ? null
      : m['scaleTo.result']({
          // The label, not the exact yield: amounts come from 7.4 so the flour is the 370 g said, and "7.4 servings" is not a sentence.
          yield: wordYield(yieldLabel(target), recipe)
        })
  );
</script>

<Sheet bind:open title={m['scaleTo.title']()} closeLabel={m['scaleTo.cancel']()}>
  <p class="body">{m['scaleTo.body']()}</p>

  <div class="fields">
    <Field label={m['scaleTo.ingredient']()}>
      {#snippet children({ id, describedBy, invalid })}
        <Select {id} {describedBy} {invalid} bind:value={chosen} options={choices} />
      {/snippet}
    </Field>

    <Field label={m['scaleTo.amount']()} hint={reading} error={problem}>
      {#snippet children({ id, describedBy, invalid })}
        <TextInput {id} {describedBy} {invalid} bind:value={typed} placeholder={example} />
      {/snippet}
    </Field>
  </div>

  <!-- Shows the answer before any commitment. -->
  {#if resultText}
    <p class="result" role="status">{resultText}</p>
  {/if}

  {#snippet footer()}
    <Button onclick={() => (open = false)}>{m['scaleTo.cancel']()}</Button>

    <Button
      variant="primary"
      disabled={target === null}
      onclick={() => {
        if (target !== null) {
          onapply(target);
          open = false;
        }
      }}
    >
      {m['scaleTo.apply']()}
    </Button>
  {/snippet}
</Sheet>

<style>
  .body {
    color: var(--text-muted);
  }

  .fields {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
    margin-top: var(--space-4);
  }

  .result {
    margin-top: var(--space-4);
    font-weight: var(--weight-medium);
  }
</style>
