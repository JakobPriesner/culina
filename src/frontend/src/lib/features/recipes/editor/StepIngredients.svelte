<script lang="ts">
  import { Button, Checkbox, IconButton, Popover } from '$ds';

  import { m } from '$shell/i18n';
  import { preferences } from '$shell/preferences.svelte';
  import { formatQuantity } from '../formatQuantity';
  import { quantityLabels } from '../quantityLabels';
  import { scaleQuantity } from '../scaling';
  import type { Ingredient, Step } from '../types';
  import { namedIn } from './stepUsage';

  /**
   * What one step needs.
   *
   * The `@` in the sentence says what the step *says*; this says what the step
   * *needs*, and the second is always the larger of the two — "combine
   * everything and knead" needs five things and names none. So the sentence's
   * mentions appear here too, as chips you cannot take off: the server folds
   * them in on every save, and a remove button that quietly undid itself would
   * be worse than no button.
   *
   * Only ingredients the server has given an id can be picked, which is the
   * same rule the mention picker follows — before the first save a new line has
   * no id for a step to point at.
   */
  interface Props {
    step: Step;
    number: number;
    /** Every ingredient in the recipe, in the order the list shows them. */
    ingredients: readonly Ingredient[];
    onchange: (uses: string[]) => void;
  }

  let { step, number, ingredients, onchange }: Props = $props();

  const named = $derived(namedIn(step));
  const needs = (id: string) => step.uses.includes(id);

  /** Saved lines only, in the recipe's order — the order a chip row reads in. */
  const choosable = $derived(ingredients.filter((one) => one.id));
  const chips = $derived(choosable.filter((one) => needs(one.id)));

  // The base amount, as the ingredient list beside it shows: the editor writes
  // a recipe, and a recipe is written at its own yield.
  const amountOf = (ingredient: Ingredient) =>
    formatQuantity(scaleQuantity(ingredient.quantity, 1), preferences.locale, quantityLabels).text;

  function set(ingredient: Ingredient, wanted: boolean) {
    // Written back in the recipe's order rather than the order they were
    // ticked, so the chips do not shuffle as you pick them.
    onchange(
      choosable
        .filter((one) => (one.id === ingredient.id ? wanted : needs(one.id)))
        .map((one) => one.id)
    );
  }
</script>

<div class="needs" role="group" aria-label={m['editor.stepIngredients']({ number })}>
  <ul class="chips">
    {#each chips as ingredient (ingredient.id)}
      {@const amount = amountOf(ingredient)}
      {@const inTheText = named.has(ingredient.id)}

      <li class="chip" class:named={inTheText}>
        {#if inTheText}
          <!-- Named in the sentence, so it is live-connected to the text -->
          <span class="badge" title={m['editor.namedInStep']({ name: ingredient.name })}>@</span>
        {/if}
        {#if amount}<span class="amount">{amount}</span>{/if}
        <span class="name">{ingredient.name}</span>

        {#if !inTheText}
          <IconButton
            label={m['editor.removeStepIngredient']({ name: ingredient.name })}
            size="sm"
            onclick={() => set(ingredient, false)}
          >
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="m6 6 12 12M18 6 6 18" stroke-linecap="round" />
            </svg>
          </IconButton>
        {/if}
      </li>
    {/each}
  </ul>

  <Popover>
    {#snippet trigger({ popovertarget })}
      <Button size="sm" {popovertarget}>{m['editor.addStepIngredient']()}</Button>
    {/snippet}

    <fieldset class="picker">
      <legend class="legend">{m['editor.pickStepIngredients']({ number })}</legend>

      {#if choosable.length > 0}
        {#each choosable as ingredient (ingredient.id)}
          {@const inTheText = named.has(ingredient.id)}

          <Checkbox
            checked={needs(ingredient.id)}
            label={ingredient.name}
            disabled={inTheText}
            onchange={(checked) => set(ingredient, checked)}
          />
        {/each}
      {:else}
        <p class="empty">{m['editor.noIngredientsYet']()}</p>
      {/if}
    </fieldset>
  </Popover>
</div>

<style>
  .needs {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-2);
    margin-block-start: var(--space-2);
  }

  .chips {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2);
    margin: 0;
    padding: 0;
    list-style: none;
  }

  .chip {
    display: inline-flex;
    align-items: center;
    gap: var(--space-2);
    padding: 0.2rem var(--space-3);
    border: 1px solid var(--border);
    border-radius: var(--radius-full);
    background: var(--surface-raised);
    box-shadow: var(--shadow-card);
    font-size: var(--text-sm);
    line-height: var(--leading-normal);
    transition:
      background-color var(--duration-fast) var(--ease-out),
      border-color var(--duration-fast) var(--ease-out),
      box-shadow var(--duration-fast) var(--ease-out);
  }

  /* A chip the sentence already carries: marked with an Apple accent tint and badge */
  .chip.named {
    background: var(--surface-accent-subtle);
    border-color: var(--border-accent);
  }

  .badge {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    width: 1.125rem;
    height: 1.125rem;
    border-radius: var(--radius-full);
    background: var(--surface-highlight);
    color: var(--accent);
    font-size: 0.6875rem;
    font-weight: var(--weight-semibold);
    margin-inline-start: -0.25rem;
  }

  .amount {
    color: var(--text-muted);
    font-variant-numeric: tabular-nums;
    font-weight: var(--weight-medium);
    white-space: nowrap;
  }

  .name {
    color: var(--text);
  }

  .picker {
    display: flex;
    flex-direction: column;
    min-width: 0;
    width: 14rem;
    max-width: 100%;
    max-height: 18rem;
    margin: 0;
    padding: 0;
    border: none;
    overflow-y: auto;
    /* Gutter for a bar that takes width, padding for an overlay bar that does
       not and is painted over the amounts on the right instead. See
       RecipePicker for why both are needed. */
    scrollbar-gutter: stable;
    padding-inline-end: var(--space-2);
    overscroll-behavior: contain;
  }

  .legend {
    padding: var(--space-1) var(--space-2) var(--space-2);
    color: var(--text-subtle);
    font-size: var(--text-xs);
    letter-spacing: 0.08em;
    text-transform: uppercase;
  }

  .empty {
    margin: 0;
    padding: var(--space-2);
    color: var(--text-muted);
    font-size: var(--text-sm);
  }
</style>
