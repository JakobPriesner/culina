<script lang="ts">
  import { Button, Field, Select, Sheet, TextInput } from '$ds';

  import { m } from '$shell/i18n';
  import { targetYieldForAmount, yieldLabel } from '../scaling';
  import { wordYield } from '../yieldWords';
  import { parseIngredientLine } from '../editor/parseIngredientLine';
  import type { Recipe } from '../types';

  /**
   * "I have 600 g of flour" — the whole recipe reshapes around it.
   *
   * The real human moment: a leftover bag, an odd package size. It is the same
   * scaling machinery driven from the other end, so it introduces no new
   * concept and cannot disagree with the stepper.
   *
   * The amount is typed as one line for the same reason the editor is: "600 g"
   * is how a person says it, and two fields would be two decisions.
   */
  interface Props {
    open: boolean;
    recipe: Recipe;
    onapply: (targetYield: number) => void;
  }

  let { open = $bindable(), recipe, onapply }: Props = $props();

  const choices = $derived(
    recipe.groups
      .flatMap((group) => group.ingredients)
      .filter((one) => one.quantity.value !== null)
      .map((one) => ({ value: one.id, label: one.name }))
  );

  let chosen = $state('');
  let typed = $state('');

  const ingredient = $derived(
    recipe.groups
      .flatMap((group) => group.ingredients)
      .find((one) => one.id === (chosen || choices[0]?.value))
  );

  const target = $derived.by(() => {
    if (!ingredient || !typed.trim()) {
      return null;
    }

    const parsed = parseIngredientLine(typed);

    return targetYieldForAmount(ingredient.quantity, parsed.quantity, recipe.yieldAmount);
  });

  const resultText = $derived(
    target === null
      ? null
      : m['scaleTo.result']({
          // The label, not the exact yield: the amounts are computed from
          // 7.4 so the flour comes out at the 370 g somebody said they had,
          // and "7.4 servings" is not a sentence.
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

    <Field label={m['scaleTo.amount']()}>
      {#snippet children({ id, describedBy, invalid })}
        <TextInput {id} {describedBy} {invalid} bind:value={typed} placeholder="600 g" />
      {/snippet}
    </Field>
  </div>

  <!-- The answer before the commitment: nobody should have to apply a change
       to find out what it does. -->
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
