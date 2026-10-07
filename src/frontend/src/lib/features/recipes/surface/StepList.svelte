<script lang="ts">
  import { m } from '$shell/i18n';
  import type { Ingredient, Step } from '../types';
  import { followCurrentStep } from './followCurrentStep.svelte';
  import IngredientList from './IngredientList.svelte';
  import type { Scaling } from './scaled.svelte';
  import SectionHead from './SectionHead.svelte';
  import StepNeeds from './StepNeeds.svelte';
  import StepText from './StepText.svelte';

  /**
   * The method: every step, with the one being cooked growing in place.
   *
   * It is one arrangement at three weightings of attention. Reading, a step is
   * text; beside each step, in the column the whole ingredient list would
   * otherwise fill, is what that step needs, when the reader asked for it by
   * step; cooking, the whole step is the control.
   */
  interface Props {
    steps: readonly Step[];
    cooking: boolean;
    /** Which step is being cooked, when cooking. */
    currentStep: number;
    /** Whether each step carries its own ingredients beside it. */
    perStep: boolean;
    /** Whether there is more than one step to deal ingredients out between. */
    divisible: boolean;
    scaling: Scaling;
    /** Which ingredient the reader is pointing at. */
    highlighted: string | null;
    /** Every ingredient the recipe has, in its own order. */
    written: readonly Ingredient[];
    /** What one step needs, as the ingredient lines themselves. */
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
  <!-- The same wrapper the ingredients heading sits in, so the two
       headings line up across the columns whether or not this one has a
       control beside it. -->
  <SectionHead title={m['recipe.steps']()} />

  {#if steps.length > 0}
    <ol class="list" bind:this={list}>
      {#each steps as step, index (step.id ?? index)}
        {@const needs = needsOf(step)}

        <li class="step" class:current={cooking && index === currentStep}>
          <!-- Beside the step, in the column the whole list would
               otherwise fill — which is the arrangement's entire point:
               what step two needs is level with step two. -->
          {#if perStep && needs.length > 0}
            <div class="step-needs">
              <IngredientList ingredients={needs} {scaling} {highlighted} onhover={onhighlight} />
            </div>
          {/if}

          <!--
            While cooking the whole step is the control, because the gesture
            that matters is "next". While reading it is text, so that the
            ingredient references inside it can be pointed at — one or the
            other, never a button inside a button.
          -->
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

              <!-- Under the step's own title and above its words, because
                   that is the order the step is carried out in: get these
                   out, then do this. At the end it was an afterthought to
                   a sentence already read, and the whole point of it is to
                   be read first.

                   Reading is planning: this is the step's own gathering
                   list, at the amounts on screen. Cooking is doing, and
                   there the panel to the left has already become it — as
                   has the column beside this step, once the reader has
                   asked for the ingredients by step. A lone step's list
                   is the panel beside it, so saying it twice says
                   nothing. -->
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

  /*
   * The method has no box, so its heading carries the inset the panel's box
   * gives the heading beside it. Without it the two headings sit a card's
   * padding apart, which is the kind of misalignment that is invisible in a
   * component and obvious on the page. The block only: the inline edge has to
   * stay level with the step text underneath it.
   */
  .steps > :global(.section-head) {
    padding-block-start: var(--card-padding);
    min-height: calc(var(--control-lg) + var(--card-padding));
  }

  /*
   * Beside the steps, the two columns are shared rather than owned.
   *
   * The panel keeps its column and its card — it is still where "Zutaten" is
   * answered — and the method's heading stays level with it, so the page opens
   * on the same two words in the same places as it does in the other
   * arrangement. What changes underneath: the list has been dealt out to the
   * steps, so each row below the headings has to reach across both columns.
   *
   * `display: contents` is what lets it. The steps section stops being a box
   * and its heading and its list become items of the grid above, which is the
   * only way a row of that list can start in the panel's column while the
   * heading above it stays in the method's.
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
   * The step and what it needs, on one row.
   *
   * The body's own two tracks, borrowed rather than restated, so the
   * ingredients stay under the heading that names them and the method stays
   * where it was. The text column is placed explicitly because a step that
   * needs nothing has no first cell to push it across.
   */
  .perStep .step {
    grid-column: 1 / -1;
    display: grid;
    grid-template-columns: subgrid;
    align-items: start;
  }

  /*
   * The surface belongs to the ingredients, not to the row.
   *
   * The section spans both columns here so a step and its ingredients can
   * share one — but a background running under the whole row would put the
   * method on a panel too, and the method is prose: read straight down, once.
   * So the card moves in one level and onto the left, where it carries on down
   * the page from the panel at the top of that column. The same material in
   * the same column means the same thing in both arrangements: this is what
   * you need out.
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
   * The current step grows in place. Its neighbours stay where they are and
   * stay readable — quieter, not faded, because the step you just finished is
   * the one you most often need to glance back at, and at 0.45 opacity it was
   * 2.7:1 against the page. Readable has a number, and that was not it.
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
    /* Nothing left to share, so the section is a section again and a step is
       a step with its ingredients under it. */
    .perStep {
      display: block;
    }

    /* Stacked, so there is no heading in the next column to line up with. */
    .steps > :global(.section-head) {
      padding-block-start: 0;
      min-height: var(--control-lg);
    }

    .perStep .step {
      display: flex;
      flex-direction: column;
      gap: var(--space-4);
    }

    /* Under the step rather than beside it, because that is the room there is
       — and under it rather than over it, so the step's own number still
       introduces the step. The one place the two arrangements disagree about
       where "what this step needs" sits: a card between a step's title and its
       words would have to be a child of the step body, and here it is a cell
       of the row beside it. */
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
