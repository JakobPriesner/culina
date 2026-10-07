<script lang="ts">
  import Self from './StepInline.svelte';
  import IngredientQuickLook from './IngredientQuickLook.svelte';
  import type { Scaling } from './scaled.svelte';
  import type { Inline } from './stepMarkdown';
  import type { Ingredient } from '../types';

  /**
   * Formatted pieces of one paragraph or list item; recursive because emphasis nests.
   * Whitespace is significant (`white-space: pre-wrap`), so tags are packed, and a reference without an amount is its own branch because Svelte trims a trailing space.
   * References and links render as plain text in non-interactive steps, where a button inside a button is invalid.
   */
  interface Props {
    nodes: readonly Inline[];
    scaling: Scaling;
    interactive: boolean;
    onhighlight?: (ingredientId: string | null) => void;
    /** The lit ingredient (bidirectional with the list). */
    highlighted?: string | null;
    /** For quick-look metadata and totals. */
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

<!-- Links in a step are off site by construction; resolve() is for app routes. -->
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
  /* Marked words, not a control (a full box read as a form field): pale wash, baseline edge, weight on the amount.
     Colour-only hover that lets go slowly; a button's own tight line-height keeps lines with references no taller. */
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
      background-color var(--duration-slow) ease,
      box-shadow var(--duration-slow) ease;
  }

  .ingredient .amount {
    font-weight: var(--weight-semibold);
    font-variant-numeric: tabular-nums;
    white-space: nowrap;
  }

  .ingredient:focus-visible {
    outline: 2px solid var(--border-focus);
    outline-offset: 1px;
  }

  /* Pointed at (here or from the ingredient list) deepens the wash and firms the edge inside the capsule, not as a ring that would read as focus; the open quick look stays lit. */
  .ingredient:hover,
  .ingredient.is-highlighted {
    background: var(--surface-highlight);
    box-shadow: inset 0 -2px 0 var(--accent);
    transition-duration: var(--duration-fast);
    transition-timing-function: var(--ease-out);
  }

  .ingredient-plain {
    display: inline;
    font-weight: var(--weight-semibold);
  }

  /* Emphasis shares the ingredient weight, so a sentence has one kind of bold. */
  strong {
    font-weight: var(--weight-semibold);
  }

  /* System monospace rather than a token: nothing else sets code. */
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

  /* On paper a reference is just its words; the button only exists to light up its list line. */
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
