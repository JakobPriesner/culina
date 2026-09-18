<script lang="ts">
  import Self from './StepInline.svelte';
  import type { Scaling } from './scaled.svelte';
  import type { Inline } from './stepMarkdown';

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
  }

  let { nodes, scaling, interactive, onhighlight }: Props = $props();
</script>

<!-- A link in a step goes off site by construction: the parser only keeps the
     ones that do, and resolve() is for this app's own routes. -->
<!-- eslint-disable svelte/no-navigation-without-resolve -->
{#each nodes as node, index (index)}{#if node.kind === 'text'}{node.text}{:else if node.kind === 'ingredient'}{#if interactive}<button
        class="ingredient"
        type="button"
        onmouseenter={() => onhighlight?.(node.ingredientId)}
        onmouseleave={() => onhighlight?.(null)}
        onfocus={() => onhighlight?.(node.ingredientId)}
        onblur={() => onhighlight?.(null)}>{scaling.show(node.quantity).text} {node.name}</button
      >{:else}<strong class="ingredient-plain"
        >{scaling.show(node.quantity).text} {node.name}</strong
      >{/if}{:else if node.kind === 'code'}<code>{node.text}</code
    >{:else if node.kind === 'link'}{#if interactive}<a
        href={node.href}
        rel="noreferrer nofollow"
        target="_blank"><Self nodes={node.children} {scaling} {interactive} {onhighlight} /></a
      >{:else}<Self
        nodes={node.children}
        {scaling}
        {interactive}
        {onhighlight}
      />{/if}{:else if node.kind === 'strong'}<strong
      ><Self nodes={node.children} {scaling} {interactive} {onhighlight} /></strong
    >{:else if node.kind === 'emphasis'}<em
      ><Self nodes={node.children} {scaling} {interactive} {onhighlight} /></em
    >{:else}<s><Self nodes={node.children} {scaling} {interactive} {onhighlight} /></s>{/if}{/each}

<style>
  /*
   * A button, not a span: pointing at an ingredient lights it up in the list,
   * and a keyboard has to be able to do the same thing. Styled as text because
   * it is text — the underline says it responds without making a sentence look
   * like a toolbar.
   */
  .ingredient {
    padding: 0;
    border: none;
    background: none;
    color: inherit;
    font: inherit;
    font-weight: var(--weight-semibold);
    text-decoration: underline;
    text-decoration-color: var(--border-strong);
    text-decoration-thickness: 1px;
    text-underline-offset: 0.2em;
    cursor: pointer;
  }

  .ingredient:hover {
    text-decoration-color: var(--accent);
  }

  .ingredient-plain {
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
      border: 0;
      background: none;
      color: inherit;
      font: inherit;
      font-weight: var(--weight-medium);
      text-decoration: none;
    }
  }
</style>
