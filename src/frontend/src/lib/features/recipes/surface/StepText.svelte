<script lang="ts">
  import type { Scaling } from './scaled.svelte';
  import type { Step } from '../types';

  /**
   * One step, with its ingredients written into the sentence at the scaled
   * amount.
   *
   * "Melt **180 g butter** in the pan" — not "Melt 200 g butter" beside an
   * ingredient list that says 180. Getting that wrong is the single most common
   * bug in recipe apps, and it is impossible here because the step stores a
   * reference rather than the words.
   */
  interface Props {
    step: Step;
    scaling: Scaling;
    /**
     * Whether an ingredient reference can be pointed at.
     *
     * Off while cooking, where the whole step is the control — a button inside
     * a button is invalid HTML and behaves unpredictably, and mid-cook the
     * useful gesture is "next step", not "which butter".
     */
    interactive?: boolean;
    /** Called as the reader's eye moves, so the ingredient list can light up. */
    onhighlight?: (ingredientId: string | null) => void;
  }

  let { step, scaling, interactive = true, onhighlight }: Props = $props();
</script>

<p class="text">
  {#each step.segments as segment, index (index)}
    {#if segment.kind === 'text'}{segment.text}{:else if interactive}<button
        class="ingredient"
        type="button"
        onmouseenter={() => onhighlight?.(segment.ingredientId)}
        onmouseleave={() => onhighlight?.(null)}
        onfocus={() => onhighlight?.(segment.ingredientId)}
        onblur={() => onhighlight?.(null)}
        >{scaling.show(segment.quantity).text} {segment.name}</button
      >{:else}<strong class="ingredient-plain"
        >{scaling.show(segment.quantity).text} {segment.name}</strong
      >{/if}
  {/each}
</p>

<style>
  /*
   * The line breaks in a step are the cook's own. A step written as three
   * lines — bake, rest, slice — is three lines because somebody meant it to
   * be, and collapsing them into a paragraph loses the shape of the
   * instruction. Imported steps depend on this too: a Tandoor instruction is
   * Markdown rendered with a line break per newline, so its newlines arrive
   * here meaning exactly that.
   *
   * It makes the whitespace in this file's markup significant, which is why
   * the references above are written without a break inside them.
   */
  .text {
    line-height: var(--leading-relaxed);
    white-space: pre-wrap;
  }

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
