<script lang="ts">
  import { Field, IconButton, TextInput } from '$ds';
  import { m } from '$shell/i18n';

  import type { Ingredient, Step } from '../types';
  import MentionField from './MentionField.svelte';
  import { toSegments, toText } from './mentions';
  import { secondsFromMinutes } from './stepEdits';
  import StepIngredients from './StepIngredients.svelte';

  /**
   * One step: its name, its sentence, its timer and what it needs.
   *
   * Every edit is reported as the whole changed step, so the list around it
   * never has to know which part of a step was touched.
   */
  interface Props {
    step: Step;
    index: number;
    /** How many steps there are, so the last one cannot be moved down. */
    count: number;
    ingredients: readonly Ingredient[];
    onchange: (step: Step) => void;
    onmove: (by: number) => void;
    onremove: () => void;
    onaddingredient: (name: string) => void;
  }

  let { step, index, count, ingredients, onchange, onmove, onremove, onaddingredient }: Props =
    $props();
</script>

<li class="step">
  <div class="head">
    <div class="name">
      <TextInput
        id="step-{index}-title"
        quiet
        label={m['editor.stepTitle']({ number: index + 1 })}
        placeholder={m['editor.stepTitlePlaceholder']({ number: index + 1 })}
        maxlength={120}
        value={step.title ?? ''}
        oninput={(title) => onchange({ ...step, title: title.trim() ? title : null })}
      />
    </div>

    <div class="controls">
      <IconButton
        label={m['editor.moveStepUp']({ number: index + 1 })}
        size="sm"
        disabled={index === 0}
        onclick={() => onmove(-1)}
      >
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <path d="m6 14 6-6 6 6" stroke-linecap="round" stroke-linejoin="round" />
        </svg>
      </IconButton>

      <IconButton
        label={m['editor.moveStepDown']({ number: index + 1 })}
        size="sm"
        disabled={index === count - 1}
        onclick={() => onmove(1)}
      >
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <path d="m6 10 6 6 6-6" stroke-linecap="round" stroke-linejoin="round" />
        </svg>
      </IconButton>

      <IconButton
        label={m['editor.removeStep']({ number: index + 1 })}
        size="sm"
        onclick={onremove}
      >
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <path d="m6 6 12 12M18 6 6 18" stroke-linecap="round" />
        </svg>
      </IconButton>
    </div>
  </div>

  <MentionField
    id="step-{index}"
    label={m['editor.stepLabel']({ number: index + 1 })}
    value={toText(step)}
    {ingredients}
    oninput={(text) => onchange({ ...step, segments: toSegments(text, ingredients) })}
    onadd={onaddingredient}
  />

  <div class="timer">
    <Field
      id="step-{index}-duration"
      label={m['editor.stepDuration']({ number: index + 1 })}
      hint={m['editor.stepDuration.hint']()}
      optionalText={m['editor.optional']()}
    >
      {#snippet children({ id, describedBy, invalid })}
        <div class="duration-control">
          <TextInput
            {id}
            type="number"
            inputmode="decimal"
            min={0.1}
            max={1440}
            step={0.5}
            value={step.durationSeconds === null ? '' : String(step.durationSeconds / 60)}
            {describedBy}
            {invalid}
            oninput={(value) => onchange({ ...step, durationSeconds: secondsFromMinutes(value) })}
          />
          <span>{m['editor.stepDuration.unit']()}</span>
        </div>
      {/snippet}
    </Field>
  </div>

  <StepIngredients
    {step}
    number={index + 1}
    {ingredients}
    onchange={(uses) => onchange({ ...step, uses })}
  />
</li>

<style>
  .step {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
    min-width: 0;
    padding: var(--space-4);
    /* Cleared of the app's floating header: an ingredient's "in step 3" jumps
       here, and landing with the step under the navbar helps nobody. */
    scroll-margin-top: var(--space-24);
  }

  .head {
    display: flex;
    /* Wraps only when it has to. The three buttons keep their size, so at 200%
       text they are 280px of a 320px screen and the title beside them cannot
       fit — and a row that will not wrap makes the page scroll sideways
       instead. At every ordinary size this changes nothing. */
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-2);
    min-width: 0;
  }

  /* The title takes the room the number used to, and the three buttons keep
     theirs: a step is named far more often than it is moved. */
  .name {
    flex: 1;
    min-width: 0;
  }

  /*
   * Set as the recipe will read it back.
   *
   * The reading surface puts a small accented line over every step — the step's
   * name, or its number when it has none — so that is what this field is. It
   * was a full-height bordered text box, which made the most optional field in
   * the editor the loudest thing in every step.
   */
  .name :global(.ds-control) {
    height: var(--control-sm);
    padding-inline: var(--space-2);
    margin-inline-start: calc(var(--space-2) * -1);
    color: var(--accent);
    font-size: var(--text-xs);
    font-weight: var(--weight-semibold);
    letter-spacing: 0.08em;
    text-transform: uppercase;
  }

  .name :global(.ds-control::placeholder) {
    color: var(--text-subtle);
    text-transform: uppercase;
  }

  .controls {
    display: flex;
    /* And the buttons wrap within it. Giving the group its own line is not
       enough on its own: three of them are 280px at 200% text, which is wider
       than a 320px screen once the step's padding is counted. */
    flex-wrap: wrap;
    justify-content: flex-end;
    flex: none;
    /* `flex: none` sizes this to its contents, so without a bound it never
       reaches the point of wrapping — it just gets wider than the screen. */
    max-width: 100%;
    gap: var(--space-1);
  }

  .timer {
    max-width: 16rem;
  }

  .duration-control {
    display: grid;
    grid-template-columns: minmax(0, 7rem) auto;
    align-items: center;
    gap: var(--space-2);
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  /* Three buttons per step is nine down a three-step recipe, and a method that
     reads as a toolbar. They belong to the step being worked on; on a touch
     screen, where nothing reveals them, they stay. */
  @media (hover: hover) {
    .controls {
      opacity: 0;
      transition: opacity var(--duration-fast) var(--ease-out);
    }

    .step:hover .controls,
    .step:focus-within .controls {
      opacity: 1;
    }
  }
</style>
