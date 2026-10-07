<script lang="ts">
  import { m } from '$shell/i18n';
  import type { Ingredient, Step } from '../types';
  import { followCurrentStep } from './followCurrentStep.svelte';
  import IngredientList from './IngredientList.svelte';
  import type { Scaling } from './scaled.svelte';
  import SectionHead from './SectionHead.svelte';
  import StepNeeds from './StepNeeds.svelte';
  import StepText from './StepText.svelte';

  /** The method: every step, the one being cooked growing in place. */
  interface Props {
    steps: readonly Step[];
    cooking: boolean;
    currentStep: number;
    perStep: boolean;
    /** Whether there is more than one step to split ingredients between. */
    divisible: boolean;
    scaling: Scaling;
    highlighted: string | null;
    written: readonly Ingredient[];
    needsOf: (step: Step) => Ingredient[];
    onstep?: (index: number) => void;
    onhighlight: (ingredientId: string | null) => void;
    onlocate: (ingredientId: string) => void;
  }

  let {
    steps,
    cooking,
    currentStep,
    perStep,
    divisible,
    scaling,
    highlighted,
    written,
    needsOf,
    onstep,
    onhighlight,
    onlocate
  }: Props = $props();

  let list = $state<HTMLOListElement>();

  followCurrentStep({
    list: () => list,
    cooking: () => cooking,
    current: () => currentStep
  });
</script>

<section class="steps" class:cooking class:perStep aria-label={m['recipe.steps']()}>
  <SectionHead title={m['recipe.steps']()} />

  {#if steps.length > 0}
    <ol class="list" bind:this={list}>
      {#each steps as step, index (step.id ?? index)}
        {@const needs = needsOf(step)}

        <li class="step" class:current={cooking && index === currentStep}>
          {#if perStep && needs.length > 0}
            <div class="step-needs">
              <IngredientList ingredients={needs} {scaling} {highlighted} onhover={onhighlight} />
            </div>
          {/if}

          <!-- Cooking: the step is the button. Reading: plain text, so ingredient references inside
               can be pointed at (no button in a button). -->
          {#if cooking}
            <button
              class="step-body"
              type="button"
              aria-current={index === currentStep ? 'step' : undefined}
              onclick={() => onstep?.(index)}
            >
              <span class="number">{step.title ?? m['recipe.step']({ number: index + 1 })}</span>
              <StepText {step} {scaling} interactive={false} />
            </button>
          {:else}
            <div class="step-body">
              <span class="number">{step.title ?? m['recipe.step']({ number: index + 1 })}</span>

              <!-- Planning list goes before the words: get these out, then do this. Per-step and
                   lone-step lists are already shown beside it. -->
              {#if !perStep && divisible && needs.length > 0}
                <StepNeeds ingredients={needs} {scaling} />
              {/if}

              <StepText
                {step}
                {scaling}
                {highlighted}
                {onhighlight}
                recipeIngredients={written}
                {onlocate}
              />
            </div>
          {/if}
        </li>
      {/each}
    </ol>
  {:else}
    <p class="empty">{m['recipe.noSteps']()}</p>
  {/if}
</section>

<style>
  .steps {
    min-width: 0;
  }

  /* No box here, so the heading takes the panel card's block inset to line up with it. */
  .steps > :global(.section-head) {
    padding-block-start: var(--card-padding);
    min-height: calc(var(--control-lg) + var(--card-padding));
  }

  /*
   * Per-step: `display: contents` lets each row start in the panel's column while the heading stays
   * in the method's.
   */
  .perStep {
    display: contents;
  }

  .perStep > :global(.section-head) {
    grid-column: 2;
    grid-row: 1;
  }

  .perStep > .list,
  .perStep > .empty {
    grid-column: 1 / -1;
    grid-row: 2;
  }

  .perStep > .list {
    display: grid;
    grid-template-columns: subgrid;
  }

  .list {
    margin: 0;
    padding: 0;
    list-style: none;
  }

  .step {
    /* Clear of the page's own floating controls too; see `--controls-inset`. */
    scroll-margin-block: var(--step-top-inset) calc(var(--bottom-inset) + var(--controls-inset));
    padding-block: var(--space-6);
    border-top: 1px solid var(--border);
  }

  /*
   * One row on the body's two subgrid tracks; the text column is placed explicitly because a step
   * with no needs has no first cell.
   */
  .perStep .step {
    grid-column: 1 / -1;
    display: grid;
    grid-template-columns: subgrid;
    align-items: start;
  }

  /*
   * The card sits on the ingredients only; a background under the whole row would put the prose
   * method on a panel.
   */
  .perStep .step-needs {
    grid-column: 1;
    padding: var(--card-padding);
    background: var(--surface-sunken);
    border-radius: var(--radius-lg);
  }

  .perStep .step-body {
    grid-column: 2;
  }

  .step-body {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
    line-height: var(--leading-relaxed);
    width: 100%;
    padding: 0;
    border: none;
    background: none;
    color: inherit;
    font: inherit;
    text-align: start;
    cursor: pointer;
  }

  .number {
    color: var(--accent);
    font-size: var(--text-xs);
    letter-spacing: 0.08em;
    text-transform: uppercase;
  }

  /*
   * Neighbours stay readable (muted, not faded): 0.45 opacity was 2.7:1, and the step just finished
   * is glanced at most.
   */
  .cooking .step {
    color: var(--text-muted);
    transition:
      color var(--duration-base) var(--ease-out),
      font-size var(--duration-base) var(--ease-spatial);
  }

  .cooking .step.current {
    color: var(--text);
    font-size: var(--text-cook);
    line-height: var(--leading-normal);
  }

  .empty {
    color: var(--text-muted);
  }

  @media screen and (width < 64rem) {
    .perStep {
      display: block;
    }

    .steps > :global(.section-head) {
      padding-block-start: 0;
      min-height: var(--control-lg);
    }

    .perStep .step {
      display: flex;
      flex-direction: column;
      gap: var(--space-4);
    }

    /*
     * Below the step so its number still introduces it; the card is a sibling cell, so it cannot
     * sit between title and words.
     */
    .perStep .step-needs {
      order: 1;
    }
  }

  @media (prefers-reduced-motion: reduce) {
    .cooking .step {
      transition: none;
    }
  }

  @media print {
    .perStep .step {
      gap: 8mm;
    }

    /* Ink, not three grey blocks. */
    .perStep .step-needs {
      padding: 0;
      background: none;
    }

    .steps > :global(.section-head) {
      padding-block-start: 0;
    }

    .steps .list {
      gap: 4mm;
    }
  }
</style>
