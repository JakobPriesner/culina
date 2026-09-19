<script lang="ts">
  import type { Scaling } from './scaled.svelte';
  import StepInline from './StepInline.svelte';
  import { parseStep } from './stepMarkdown';
  import type { Ingredient, Step } from '../types';

  /**
   * One step, written as Markdown, with its ingredients written into the
   * sentence at the scaled amount.
   *
   * "Melt **180 g butter** in the pan" — not "Melt 200 g butter" beside an
   * ingredient list that says 180. Getting that wrong is the single most common
   * bug in recipe apps, and it is impossible here because the step stores a
   * reference rather than the words.
   *
   * The Markdown is parsed over those references rather than around them, so
   * emphasis and lists never cost a step its scaling. See `stepMarkdown.ts`.
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
    /** Which ingredient is highlighted on the surface (bidirectional) */
    highlighted?: string | null;
    /** All recipe ingredients for quick-look metadata and totals */
    recipeIngredients?: readonly Ingredient[];
    onlocate?: (ingredientId: string) => void;
  }

  let {
    step,
    scaling,
    interactive = true,
    onhighlight,
    highlighted = null,
    recipeIngredients = [],
    onlocate
  }: Props = $props();

  const blocks = $derived(parseStep(step.segments));
</script>

{#each blocks as block, index (index)}
  {#if block.kind === 'paragraph'}
    <p class="text">
      <StepInline
        nodes={block.children}
        {scaling}
        {interactive}
        {onhighlight}
        {highlighted}
        {recipeIngredients}
        {onlocate}
      />
    </p>
  {:else if block.ordered}
    <ol class="list">
      {#each block.items as item, position (position)}
        <li>
          <StepInline
            nodes={item}
            {scaling}
            {interactive}
            {onhighlight}
            {highlighted}
            {recipeIngredients}
            {onlocate}
          />
        </li>
      {/each}
    </ol>
  {:else}
    <ul class="list">
      {#each block.items as item, position (position)}
        <li>
          <StepInline
            nodes={item}
            {scaling}
            {interactive}
            {onhighlight}
            {highlighted}
            {recipeIngredients}
            {onlocate}
          />
        </li>
      {/each}
    </ul>
  {/if}
{/each}

<style>
  /*
   * The line breaks inside a paragraph are the cook's own. A step written as
   * three lines — bake, rest, slice — is three lines because somebody meant it
   * to be, and collapsing them into one loses the shape of the instruction.
   * Imported steps depend on this too: a Tandoor instruction is Markdown
   * rendered with a line break per newline, so its newlines arrive here meaning
   * exactly that. A *blank* line is the one that starts a new paragraph, which
   * is what Markdown says it is.
   *
   * It makes the whitespace inside a paragraph significant, which is why
   * `StepInline` is written without breaks between its tags.
   */
  .text {
    line-height: var(--leading-relaxed);
    white-space: pre-wrap;
  }

  /*
   * A list inside a step is nearly always a list of things to do in order, so
   * it is set as prose with markers rather than as a stack of rows: indented
   * enough to read as a list, not so much that it leaves the sentence behind.
   *
   * No margins of its own, here or on the paragraphs: a step body is a column
   * with a gap, and every block a step renders is one of its items — as the
   * step's number and the list of what it needs already are. A margin on top
   * would be added to that gap rather than replacing it.
   */
  .list {
    margin: 0;
    padding-left: var(--space-6);
    line-height: var(--leading-relaxed);
  }

  .list li + li {
    margin-top: var(--space-1);
  }
</style>
