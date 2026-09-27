<script lang="ts">
  import type { Scaling } from './scaled.svelte';
  import type { IngredientLine } from '../ingredientLines';

  /**
   * One line of the ingredient list.
   *
   * The amount is its own element so it can be highlighted when it changes —
   * the number morphs in place rather than the row being replaced, which is
   * what lets the eye see *what* changed when the servings move.
   *
   * Its two columns come from the list around it rather than from the row, so
   * that every name starts at the same place. See `IngredientList`.
   */
  interface Props {
    line: IngredientLine;
    scaling: Scaling;
    /** Lit while the step that uses it is being read. */
    highlighted?: boolean;
    /** Emitted when row is hovered or focused, for bidirectional highlighting */
    onhover?: (ids: readonly string[] | null) => void;
  }

  let { line, scaling, highlighted = false, onhover }: Props = $props();

  const amount = $derived(scaling.amountFor(line));
</script>

<li
  id="ingredient-row-{line.ids[0]}"
  class="row"
  class:highlighted
  onmouseenter={() => onhover?.(line.ids)}
  onmouseleave={() => onhover?.(null)}
>
  <span class="amount">{amount.text}</span>

  <span class="name">
    {line.name}{#if line.note}<span class="note">, {line.note}</span>{/if}
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
   * Lit, not boxed: the row keeps its place in the list and the eye is drawn
   * to it without the layout moving a pixel.
   *
   * The band is its own layer, as tall as the row and reaching out past the
   * text on either side, rather than the row's background plus a halo — a
   * halo reached into the rows above and below, and a block that tall read as
   * a selected item rather than a line being pointed at. A light wash of the
   * accent, not a solid step up the scale: it should say "this one" without
   * becoming the loudest thing on the card.
   *
   * Everything here animates on opacity and transform alone, so a row lighting
   * up never lays anything out again. Slow to let go: sweeping the pointer
   * along a step leaves a short trail down the list rather than rows blinking
   * on and off. A transition takes its timing from the state it is going to,
   * so the quick way in is set on `.highlighted`; the way out is a plain
   * `ease`, because the token curve is so front-loaded that a slow fade on it
   * is over as fast as a quick one.
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
   * A mark down the edge, because a tint alone cannot be relied on here.
   *
   * The list sits on a sunken card, and in a warm palette the distance between
   * a card and a tint on that card is small in light mode however the tint is
   * chosen — an earlier one landed at a contrast ratio of 1.03, which is to say
   * nothing visibly happened at all. The accent is the one colour the theme
   * contract already proves carries against the page, so the answer that holds
   * in both modes is a line of it rather than a louder wash.
   *
   * Set in from the band's edge and short of its ends, so it reads as an
   * indicator on the band rather than a rule drawn beside it, and drawn out
   * from its middle as the row lights instead of appearing whole.
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
    /* Amount and unit already travel joined by a non-breaking space; this
       keeps the column from wrapping at all. */
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
</style>
