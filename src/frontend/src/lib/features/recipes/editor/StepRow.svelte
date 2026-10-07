<script lang="ts">
  import { Field, IconButton, TextInput } from '$ds';
  import { m } from '$shell/i18n';

  import type { Ingredient, Step } from '../types';
  import MentionField from './MentionField.svelte';
  import { toSegments, toText } from './mentions';
  import { secondsFromMinutes } from './stepEdits';
  import StepIngredients from './StepIngredients.svelte';

  // Every edit is reported as the whole changed step.
  interface Props {
    step: Step;
    index: number;
    /** Total steps, so the last one can't move down. */
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
    /* Clears the floating header when an ingredient's "in step N" link jumps here. */
    scroll-margin-top: var(--space-24);
  }

  .head {
    display: flex;
    /* Wraps for 200% text: the three buttons are 280px of a 320px screen and would force sideways scroll. */
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-2);
    min-width: 0;
  }

  .name {
    flex: 1;
    min-width: 0;
  }

  /* Styled as the reading surface's small accented step label, so this optional field isn't the loudest thing. */
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
    /* The group itself wraps too: at 200% text it is wider than a 320px screen. */
    flex-wrap: wrap;
    justify-content: flex-end;
    flex: none;
    /* `flex: none` sizes to content, so without a bound it never wraps. */
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

  /* Controls appear on hover/focus so the method doesn't read as a toolbar; on touch they stay. */
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
