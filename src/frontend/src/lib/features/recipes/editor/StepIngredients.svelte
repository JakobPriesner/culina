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
   * What one step needs: the `@` mentions say what it says, this says what it needs (always more).
   * Mentioned ingredients appear as chips that can't be removed, since the server folds them in on
   * every save; only saved lines can be picked.
   */
  interface Props {
    step: Step;
    number: number;
    ingredients: readonly Ingredient[];
    onchange: (uses: string[]) => void;
  }

  let { step, number, ingredients, onchange }: Props = $props();

  const named = $derived(namedIn(step));
  const needs = (id: string) => step.uses.includes(id);

  const choosable = $derived(ingredients.filter((one) => one.id));
  const chips = $derived(choosable.filter((one) => needs(one.id)));

  // The base amount, as the ingredient list shows it: a recipe is written at its own yield.
  const amountOf = (ingredient: Ingredient) =>
    formatQuantity(scaleQuantity(ingredient.quantity, 1), preferences.locale, quantityLabels).text;

  function set(ingredient: Ingredient, wanted: boolean) {
    // Written back in the recipe's order, so the chips don't shuffle as you pick them.
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
    /*
     * Without this the list keeps its widest chip's width and the page scrolls sideways at large
     * text.
     */
    min-width: 0;
    gap: var(--space-2);
    margin: 0;
    padding: 0;
    list-style: none;
  }

  .chip {
    display: inline-flex;
    /* The chip too, so a long name wraps inside it. */
    min-width: 0;
    max-width: 100%;
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
    /*
     * Gutter for a bar that takes width, padding for an overlay bar that doesn't; see RecipePicker.
     */
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
