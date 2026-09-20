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
{#each nodes as node, index (index)}{#if node.kind === 'text'}{node.text}{:else if node.kind === 'ingredient'}{#if interactive}<button
        bind:this={buttonRefs[index]}
        class="ingredient"
        class:is-highlighted={highlighted === node.ingredientId}
        type="button"
        onmouseenter={() => onhighlight?.(node.ingredientId)}
        onmouseleave={() => onhighlight?.(null)}
        onfocus={() => onhighlight?.(node.ingredientId)}
        onblur={() => onhighlight?.(null)}
        onclick={() => (activeQuickLook = activeQuickLook === index ? null : index)}
        >{scaling.show(node.quantity).text} {node.name}</button
      >{#if activeQuickLook === index && buttonRefs[index]}<IngredientQuickLook
          anchor={buttonRefs[index]}
          name={node.name}
          stepAmount={scaling.show(node.quantity).text}
          totalAmount={totalFor(node.ingredientId)}
          note={noteFor(node.ingredientId)}
          onclose={() => (activeQuickLook = null)}
          onlocate={onlocate ? () => onlocate?.(node.ingredientId) : undefined}
        />{/if}{:else}<strong class="ingredient-plain"
        >{scaling.show(node.quantity).text} {node.name}</strong
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
   * Apple-grade inline ingredient capsule:
   * A tactile, translucent pill with tabular numbers and an elegant hover glow.
   * Fits smoothly into pre-wrap running text without distorting line height.
   */
  .ingredient {
    display: inline-flex;
    align-items: baseline;
    gap: 0.25em;
    padding: 0.08em 0.45em;
    margin: -0.08em 0.1em;
    border: 1px solid var(--border-accent);
    border-radius: var(--radius-sm);
    background: var(--surface-accent-subtle);
    color: var(--text);
    font: inherit;
    line-height: inherit;
    cursor: pointer;
    vertical-align: baseline;
    transition:
      background-color var(--duration-fast) var(--ease-out),
      border-color var(--duration-fast) var(--ease-out),
      box-shadow var(--duration-fast) var(--ease-out),
      transform var(--duration-fast) var(--ease-spatial);
  }

  .ingredient:hover {
    background: var(--surface-highlight);
    border-color: var(--accent);
    transform: translateY(-0.5px);
  }

  .ingredient:active {
    transform: translateY(0.5px) scale(0.98);
  }

  .ingredient:focus-visible {
    outline: 2px solid var(--border-focus);
    outline-offset: 1px;
  }

  /*
   * Active bidirectional highlight:
   * When hovering an ingredient in the ingredients list, all mentions of it
   * across the steps illuminate with this warm, vibrant Apple glow.
   */
  .ingredient.is-highlighted {
    background: var(--surface-highlight);
    border-color: var(--accent);
    box-shadow: var(--shadow-highlight);
    transform: translateY(-0.5px);
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
      transform: none;
    }
  }
</style>
