<script lang="ts">
  import Self from './StepInline.svelte';
  import IngredientQuickLook from './IngredientQuickLook.svelte';
  import type { Scaling } from './scaled.svelte';
  import type { Inline } from './stepMarkdown';
  import type { Ingredient } from '../types';

  /**
   * The formatted pieces of one paragraph or list item.
   *
   * It renders itself for the inside of a span, because emphasis nests — and
   * the nesting is the only reason this is a component of its own rather than
   * markup inside `StepText`.
   *
   * Both an ingredient reference and a link stand down to plain text when the
   * step is not interactive, because there the whole step is a button and a
   * button inside a button is invalid HTML that behaves unpredictably.
   *
   * The whitespace in this file is significant: a step is laid out with
   * `white-space: pre-wrap`, so a line break written here for tidiness would be
   * a line break on the page. That is why the tags below are packed together.
   * Svelte also trims a space at the end of a block, which is why a reference
   * with no amount is its own branch rather than an amount that may be empty.
   */
  interface Props {
    nodes: readonly Inline[];
    scaling: Scaling;
    /** Whether an ingredient reference can be pointed at. */
    interactive: boolean;
    onhighlight?: (ingredientId: string | null) => void;
    /** Which ingredient is currently illuminated on the surface (bidirectional) */
    highlighted?: string | null;
    /** All recipe ingredients for quick-look metadata and totals */
    recipeIngredients?: readonly Ingredient[];
    onlocate?: (ingredientId: string) => void;
  }

  let {
    nodes,
    scaling,
    interactive,
    onhighlight,
    highlighted = null,
    recipeIngredients = [],
    onlocate
  }: Props = $props();

  let activeQuickLook = $state<number | null>(null);
  let buttonRefs = $state<Record<number, HTMLElement>>({});

  function totalFor(ingredientId: string): string | null {
    const ing = recipeIngredients.find((i) => i.id === ingredientId);
    return ing ? scaling.amountFor(ing).text : null;
  }

  function noteFor(ingredientId: string): string | null {
    const ing = recipeIngredients.find((i) => i.id === ingredientId);
    return ing?.note ?? null;
  }
</script>

<!-- A link in a step goes off site by construction: the parser only keeps the
     ones that do, and resolve() is for this app's own routes. -->
<!-- eslint-disable svelte/no-navigation-without-resolve -->
{#each nodes as node, index (index)}{#if node.kind === 'text'}{node.text}{:else if node.kind === 'ingredient'}{@const amount =
      scaling.show(node.quantity).text}{#if interactive}<button
        bind:this={buttonRefs[index]}
        class="ingredient"
        class:is-highlighted={highlighted === node.ingredientId || activeQuickLook === index}
        type="button"
        aria-haspopup="dialog"
        aria-expanded={activeQuickLook === index}
        onmouseenter={() => onhighlight?.(node.ingredientId)}
        onmouseleave={() => onhighlight?.(null)}
        onfocus={() => onhighlight?.(node.ingredientId)}
        onblur={() => onhighlight?.(null)}
        onclick={() => (activeQuickLook = activeQuickLook === index ? null : index)}
        >{#if amount}<span class="amount">{amount}</span>
          <span class="name">{node.name}</span>{:else}<span class="name">{node.name}</span
          >{/if}</button
      >{#if activeQuickLook === index && buttonRefs[index]}<IngredientQuickLook
          anchor={buttonRefs[index]}
          name={node.name}
          stepAmount={amount}
          totalAmount={totalFor(node.ingredientId)}
          note={noteFor(node.ingredientId)}
          onclose={() => (activeQuickLook = null)}
          onlocate={onlocate ? () => onlocate?.(node.ingredientId) : undefined}
        />{/if}{:else}<strong class="ingredient-plain"
        >{#if amount}{amount} {node.name}{:else}{node.name}{/if}</strong
      >{/if}{:else if node.kind === 'code'}<code>{node.text}</code
    >{:else if node.kind === 'link'}{#if interactive}<a
        href={node.href}
        rel="noreferrer nofollow"
        target="_blank"
        ><Self
          nodes={node.children}
          {scaling}
          {interactive}
          {onhighlight}
          {highlighted}
          {recipeIngredients}
          {onlocate}
        /></a
      >{:else}<Self
        nodes={node.children}
        {scaling}
        {interactive}
        {onhighlight}
        {highlighted}
        {recipeIngredients}
        {onlocate}
      />{/if}{:else if node.kind === 'strong'}<strong
      ><Self
        nodes={node.children}
        {scaling}
        {interactive}
        {onhighlight}
        {highlighted}
        {recipeIngredients}
        {onlocate}
      /></strong
    >{:else if node.kind === 'emphasis'}<em
      ><Self
        nodes={node.children}
        {scaling}
        {interactive}
        {onhighlight}
        {highlighted}
        {recipeIngredients}
        {onlocate}
      /></em
    >{:else}<s
      ><Self
        nodes={node.children}
        {scaling}
        {interactive}
        {onhighlight}
        {highlighted}
        {recipeIngredients}
        {onlocate}
      /></s
    >{/if}{/each}

<style>
  /*
   * An ingredient reference is part of the sentence, so it is set as marked
   * words rather than as a control dropped into them: a pale accent wash with
   * a quiet edge along the baseline, the way a highlighter marks a line on
   * paper. A box drawn all the way round made every reference read as a form
   * field, and a paragraph with four of them as a form.
   *
   * The amount carries the weight, because it is the part that changes as the
   * recipe scales and the part the eye comes back for mid-step. The name stays
   * at the paragraph's own weight — it is a word of the sentence.
   *
   * Nothing moves on hover. Lifting a word by half a pixel shifts it against
   * its neighbours on the line, and running text should hold still.
   *
   * A button is laid out as one box whatever its display says, so it sets its
   * own tighter line height: wash and padding together stay shorter than the
   * paragraph's line, and a line with a reference in it is no taller than one
   * without — nor does a reference touch the one on the line below.
   */
  .ingredient {
    display: inline-block;
    max-width: 100%;
    padding: 0.1em 0.3em;
    margin: 0 0.05em;
    border: 0;
    border-radius: var(--radius-sm);
    background: var(--surface-accent-subtle);
    box-shadow: inset 0 -1px 0 var(--border-accent);
    color: var(--text);
    font: inherit;
    line-height: var(--leading-tight);
    text-align: inherit;
    vertical-align: baseline;
    cursor: pointer;
    transition:
      background-color var(--duration-fast) var(--ease-out),
      box-shadow var(--duration-fast) var(--ease-out);
  }

  .ingredient .amount {
    font-weight: var(--weight-semibold);
    font-variant-numeric: tabular-nums;
    white-space: nowrap;
  }

  .ingredient:hover {
    background: var(--surface-highlight);
  }

  .ingredient:focus-visible {
    outline: 2px solid var(--border-focus);
    outline-offset: 1px;
  }

  /*
   * Pointed at from its line in the ingredient list, every mention of it lights
   * up at once. The one whose quick look is open stays lit while it is, so the
   * card is never left pointing at a word that looks like all the others.
   */
  .ingredient.is-highlighted {
    background: var(--surface-highlight);
    box-shadow:
      inset 0 -1px 0 var(--accent),
      var(--shadow-highlight);
  }

  .ingredient-plain {
    display: inline;
    font-weight: var(--weight-semibold);
  }

  /*
   * An emphasis in a step is somebody's warning — "do **not** stir" — so it
   * takes the same weight an ingredient does, and no more. Two kinds of bold in
   * one sentence would be a sentence arguing with itself.
   */
  strong {
    font-weight: var(--weight-semibold);
  }

  /*
   * The system monospace stack rather than a token: nothing else in the app
   * sets code, and a font nobody else needs does not belong in the scale.
   */
  code {
    padding: 0 var(--space-1);
    border-radius: var(--radius-sm);
    background: var(--surface-sunken);
    font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
    font-size: 0.9em;
  }

  a {
    color: inherit;
    text-decoration: underline;
    text-decoration-color: var(--accent);
    text-underline-offset: 0.2em;
  }

  /*
   * On paper an ingredient reference is simply the words it stands for. It is
   * a button on screen because pointing at it lights up the line it came from,
   * and there is nothing to point at on a sheet of paper.
   */
  @media print {
    .ingredient {
      display: inline;
      padding: 0;
      margin: 0;
      border: 0;
      background: none;
      color: inherit;
      box-shadow: none;
    }
  }
</style>
