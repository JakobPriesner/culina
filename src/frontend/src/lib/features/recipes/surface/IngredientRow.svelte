<script lang="ts">
  import { m } from '$shell/i18n';

  import type { Scaling } from './scaled.svelte';
  import { scales } from '../units';
  import type { IngredientLine } from '../ingredientLines';

  /**
   * One line of the ingredient list; the amount is its own element so it can be highlighted, and
   * the columns come from `IngredientList`.
   */
  interface Props {
    line: IngredientLine;
    scaling: Scaling;
    highlighted?: boolean;
    /** Still to buy on the shopping list: a small basket, so what is already there is seen before adding again. */
    listed?: boolean;
    /** Offered on a measured amount: "I have 600 g of this", scaled from here. */
    onscaleto?: (ingredientId: string) => void;
    onhover?: (ids: readonly string[] | null) => void;
  }

  let { line, scaling, highlighted = false, listed = false, onscaleto, onhover }: Props = $props();

  const amount = $derived(scaling.amountFor(line));
  /** A pinch scales nothing, so nothing can be scaled from it either. */
  const scalable = $derived(
    Boolean(onscaleto) && line.quantity.value !== null && scales(line.quantity.unit)
  );
</script>

<li
  id="ingredient-row-{line.ids[0]}"
  class="row"
  class:highlighted
  onmouseenter={() => onhover?.(line.ids)}
  onmouseleave={() => onhover?.(null)}
>
  {#if scalable && amount.text}
    <!-- The name starts with the amount it shows, so "600 g" said aloud presses it (WCAG 2.5.3). -->
    <button
      type="button"
      class="amount scale"
      aria-label={m['recipe.scaleFrom']({ amount: amount.text, name: line.name })}
      title={m['recipe.scaleFrom']({ amount: amount.text, name: line.name })}
      onclick={() => onscaleto?.(line.ids[0] ?? '')}>{amount.text}</button
    >
  {:else}
    <span class="amount">{amount.text}</span>
  {/if}

  <span class="name">
    {scaling.nameFor(line)}{#if line.note}<span class="note">, {line.note}</span
      >{/if}{#if listed}<span
        class="listed"
        role="img"
        aria-label={m['shopping.row.listed']()}
        title={m['shopping.row.listed']()}
        ><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"
          ><path
            d="M4 8h16l-1.4 10a2 2 0 0 1-2 1.7H7.4a2 2 0 0 1-2-1.7Z"
            stroke-linejoin="round"
          /><path d="m9 13.5 2 2 4-4" stroke-linecap="round" stroke-linejoin="round" /></svg
        ></span
      >{/if}
  </span>
</li>

<style>
  .row {
    position: relative;
    isolation: isolate;
    display: grid;
    grid-column: 1 / -1;
    grid-template-columns: subgrid;
    padding-block: var(--space-2);
  }

  /*
   * Lit, not boxed: the highlight is its own layer, animating opacity and transform only, so no
   * layout moves.
   * The way in is quick (set on `.highlighted`); the way out is a plain slow `ease`, as the token
   * curve is too front-loaded.
   */
  .row::before {
    content: '';
    position: absolute;
    z-index: -1;
    inset-block: 0;
    inset-inline: calc(-1 * var(--space-3));
    border-radius: var(--radius-sm);
    background: var(--surface-highlight-band);
    opacity: 0;
    transition: opacity var(--duration-slow) ease;
  }

  /*
   * An accent mark down the edge: a tint alone is not reliable, since the accent is the colour the
   * theme contract proves carries against the page.
   */
  .row::after {
    content: '';
    position: absolute;
    inset-block: var(--space-2);
    inset-inline-start: calc(-1 * var(--space-3) + 3px);
    width: 3px;
    border-radius: var(--radius-full);
    background: var(--accent);
    opacity: 0;
    transform: scaleY(0.4);
    transition:
      opacity var(--duration-slow) ease,
      transform var(--duration-slow) ease;
  }

  .highlighted::before {
    opacity: 1;
    transition-duration: var(--duration-fast);
    transition-timing-function: var(--ease-out);
  }

  .highlighted::after {
    opacity: 1;
    transform: none;
    transition-duration: var(--duration-fast), var(--duration-base);
    transition-timing-function: var(--ease-out);
  }

  .amount {
    font-variant-numeric: tabular-nums;
    font-weight: var(--weight-medium);
    /*
     * Amount and unit are already joined by a non-breaking space; this stops the column wrapping at
     * all.
     */
    white-space: nowrap;
  }

  @media print {
    .row {
      padding-block: 1mm;
    }

    .row::before,
    .row::after {
      display: none;
    }
  }

  .note {
    color: var(--text-muted);
  }

  .listed {
    display: inline-flex;
    width: 1em;
    height: 1em;
    margin-inline-start: var(--space-2);
    vertical-align: -0.125em;
    color: var(--text-success, var(--success));
  }

  .listed svg {
    width: 100%;
    height: 100%;
  }

  @media print {
    .listed {
      display: none;
    }
  }

  /* The amount, quietly a control: dotted until pointed at, so the list still reads as a list. */
  .scale {
    padding: 0;
    border: 0;
    background: none;
    color: inherit;
    font: inherit;
    text-align: inherit;
    text-decoration: underline dotted;
    text-decoration-color: var(--border-strong, currentColor);
    text-underline-offset: 0.2em;
    cursor: pointer;
  }

  @media (hover: hover) {
    .scale:hover {
      text-decoration-style: solid;
    }
  }
</style>
