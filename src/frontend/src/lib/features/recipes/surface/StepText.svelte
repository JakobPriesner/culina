<script lang="ts">
  import type { Scaling } from './scaled.svelte';
  import StepInline from './StepInline.svelte';
  import { parseStep } from './stepMarkdown';
  import type { Ingredient, Step } from '../types';

  /**
   * One step as Markdown with ingredients written in at the scaled amount; the step stores
   * references, so text and list can't disagree. Parsed over the references: see `stepMarkdown.ts`.
   */
  interface Props {
    step: Step;
    scaling: Scaling;
    /** Off while cooking: the whole step is the control, and a button inside a button is invalid HTML. */
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
   * Line breaks inside a paragraph are the cook's own (imported Tandoor steps rely on it too);
   * a blank line starts a paragraph. Whitespace is significant, so `StepInline` has no breaks
   * between its tags.
   */
  .text {
    line-height: var(--leading-relaxed);
    white-space: pre-wrap;
  }

  /*
   * A list in a step is prose with markers. No margins here or on paragraphs: the step body is a
   * column with a gap, and a margin would add to it.
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
