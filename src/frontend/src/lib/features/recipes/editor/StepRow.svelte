<script lang="ts">
  import { Button, Field, IconButton, TextInput } from '$ds';
  import { durationsIn } from '$features/cooking/stepDurations';
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
    /** Your own note on this step, from cooking it: shown here so it can be folded into the recipe. */
    note?: string;
    /** Puts the note into the step's words, for everyone who cooks it. */
    onadoptnote?: () => void;
  }

  let {
    step,
    index,
    count,
    ingredients,
    onchange,
    onmove,
    onremove,
    onaddingredient,
    note = '',
    onadoptnote
  }: Props = $props();

  /** Somewhere in this step has focus: its hints show here as well as on the first step. */
  let focused = $state(false);
  const hinted = $derived(index === 0 || focused);

  /** Waits the words name ("10 Min. köcheln"), offered as the step's timer while it has none; cook mode offers the same ones. */
  const spotted = $derived(step.durationSeconds === null ? durationsIn(toText(step)) : []);
</script>

<li class="step" onfocusin={() => (focused = true)} onfocusout={() => (focused = false)}>
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

  {#if note.trim()}
    <aside class="personal" aria-label={m['notes.editor.step']()}>
      <span class="personal-label">{m['notes.editor.step']()}</span>
      <p class="personal-text">{note}</p>
      {#if onadoptnote}
        <Button size="sm" onclick={onadoptnote}>{m['notes.editor.adopt']()}</Button>
      {/if}
    </aside>
  {/if}

  <MentionField
    id="step-{index}"
    label={m['editor.stepLabel']({ number: index + 1 })}
    value={toText(step, ingredients)}
    {ingredients}
    oninput={(text) =>
      onchange({ ...step, segments: toSegments(text, ingredients, step.segments) })}
    onadd={onaddingredient}
    showHint={hinted}
  />

  <div class="timer">
    <Field
      id="step-{index}-duration"
      label={m['editor.stepDuration']({ number: index + 1 })}
      hint={hinted ? m['editor.stepDuration.hint']() : undefined}
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

    {#if spotted.length > 0}
      <div class="spotted">
        {#each spotted as seconds (seconds)}
          {@const minutes = Math.round(seconds / 60)}
          <Button
            size="sm"
            label={m['editor.stepDuration.spotted.label']({ number: index + 1, minutes })}
            onclick={() => onchange({ ...step, durationSeconds: seconds })}
          >
            {m['editor.stepDuration.spotted']({ minutes })}
          </Button>
        {/each}
      </div>
    {/if}
  </div>

  <StepIngredients
    {step}
    number={index + 1}
    {ingredients}
    onchange={(uses) => onchange({ ...step, uses })}
    onmention={(segments) => onchange({ ...step, segments })}
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
    font-size: var(--text-sm);
    font-weight: var(--weight-semibold);
  }

  .name :global(.ds-control::placeholder) {
    color: var(--text-subtle);
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

  .spotted {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2);
    margin-top: var(--space-2);
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

  /* Yours, beside the author's words, as on the recipe page: a margin note to fold in or leave. */
  .personal {
    display: flex;
    flex-direction: column;
    align-items: flex-start;
    gap: var(--space-1);
    margin-block: var(--space-2);
    padding: var(--space-2) var(--space-3);
    border-inline-start: 3px solid var(--accent);
    border-radius: var(--radius-sm);
    background: var(--surface-sunken);
    font-size: var(--text-sm);
  }

  .personal-label {
    color: var(--text-muted);
    font-weight: var(--weight-medium);
  }

  .personal-text {
    margin: 0;
    white-space: pre-wrap;
  }
</style>
