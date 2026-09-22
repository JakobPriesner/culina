<script lang="ts">
  import { tick } from 'svelte';

  import { IconButton, TextInput } from '$ds';

  import { m } from '$shell/i18n';
  import MentionField from './MentionField.svelte';
  import StepIngredients from './StepIngredients.svelte';
  import { toSegments, toText } from './mentions';
  import type { Ingredient, Step } from '../types';

  /**
   * The steps, as sentences.
   *
   * An author writes "Melt @butter in the pan" — the `@` is the whole of the
   * ceremony, and what it buys is a step whose amounts follow the portions.
   * Naming an ingredient the recipe does not have yet adds it to the list from
   * inside the sentence, so the method can be written first and measured after.
   *
   * Under each sentence is what the step needs, which is the larger question:
   * "combine everything and knead" names nothing and needs everything, and a
   * cook standing at the counter is asking what to get out, not what the words
   * happen to mention.
   *
   * Reordering is buttons, not drag. Drag alone cannot be done with a keyboard,
   * and a recipe is rearranged rarely enough that two arrows are no hardship.
   *
   * The number above each step is a field, and it is set exactly as the recipe
   * will read it back: the small accented line the reading surface puts over
   * every step. A step in a short recipe is "step 3" and the placeholder says
   * so; a step in a layered one is "prepare the base", and typing that over the
   * number is the whole of naming it. Empty is not a name, so clearing the
   * field gives the number back.
   */
  interface Props {
    steps: readonly Step[];
    /** What a mention can point at, and what a new one is added to. */
    ingredients: readonly Ingredient[];
    onchange: (steps: Step[]) => void;
    onaddingredient: (name: string) => void;
  }

  let { steps, ingredients, onchange, onaddingredient }: Props = $props();

  function update(index: number, text: string) {
    onchange(
      steps.map((step, candidate) =>
        candidate === index ? { ...step, segments: toSegments(text, ingredients) } : step
      )
    );
  }

  function retitle(index: number, title: string) {
    onchange(
      steps.map((step, candidate) =>
        candidate === index ? { ...step, title: title.trim() ? title : null } : step
      )
    );
  }

  function setUses(index: number, uses: string[]) {
    onchange(steps.map((step, candidate) => (candidate === index ? { ...step, uses } : step)));
  }

  /**
   * Adds a step and puts the cursor in it.
   *
   * The reason anybody presses this button is to write the next sentence, and a
   * new empty box that then has to be aimed at is the app making them ask
   * twice. After a tick, because the field does not exist until the longer list
   * has rendered.
   */
  async function add() {
    const at = steps.length;

    onchange([...steps, { id: null, title: null, segments: [], uses: [], durationSeconds: null }]);

    await tick();
    document.getElementById(`step-${at}`)?.focus();
  }

  function remove(index: number) {
    onchange(steps.filter((_, candidate) => candidate !== index));
  }

  function move(index: number, by: number) {
    const to = index + by;

    if (to < 0 || to >= steps.length) {
      return;
    }

    const next = [...steps];
    const [moved] = next.splice(index, 1);

    next.splice(to, 0, moved!);
    onchange(next);
  }
</script>

<div class="editor">
  <div class="panel">
    {#if steps.length > 0}
      <ol class="list">
        {#each steps as step, index (step.id ?? index)}
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
                  oninput={(title) => retitle(index, title)}
                />
              </div>

              <div class="controls">
                <IconButton
                  label={m['editor.moveStepUp']({ number: index + 1 })}
                  size="sm"
                  disabled={index === 0}
                  onclick={() => move(index, -1)}
                >
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <path d="m6 14 6-6 6 6" stroke-linecap="round" stroke-linejoin="round" />
                  </svg>
                </IconButton>

                <IconButton
                  label={m['editor.moveStepDown']({ number: index + 1 })}
                  size="sm"
                  disabled={index === steps.length - 1}
                  onclick={() => move(index, 1)}
                >
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <path d="m6 10 6 6 6-6" stroke-linecap="round" stroke-linejoin="round" />
                  </svg>
                </IconButton>

                <IconButton
                  label={m['editor.removeStep']({ number: index + 1 })}
                  size="sm"
                  onclick={() => remove(index)}
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
              oninput={(text) => update(index, text)}
              onadd={onaddingredient}
            />

            <StepIngredients
              {step}
              number={index + 1}
              {ingredients}
              onchange={(uses) => setUses(index, uses)}
            />
          </li>
        {/each}
      </ol>
    {:else}
      <p class="none">{m['editor.stepsEmpty']()}</p>
    {/if}

    <!-- The last row of the list rather than a button beside it: adding a step
         is what you do at the bottom of the method, and a control that sits
         where the next step will appear needs no explaining. -->
    <button type="button" class="add" onclick={() => void add()}>
      <span class="plus" aria-hidden="true">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <path d="M12 5v14M5 12h14" stroke-linecap="round" />
        </svg>
      </span>
      {m['editor.addStep']()}
    </button>
  </div>
</div>

<style>
  .editor {
    min-width: 0;
  }

  /* The same enclosure the ingredients have, for the same reason: the steps and
     the row that adds one are a single thing, and a border is what says so
     without lifting the method off the page. */
  .panel {
    min-width: 0;
    border: 1px solid var(--border);
    border-radius: var(--radius-lg);
    background: var(--surface-raised);
  }

  .list {
    margin: 0;
    padding: 0;
    list-style: none;
  }

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

  .step + .step,
  .add {
    border-top: 1px solid var(--border);
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
    flex: none;
    gap: var(--space-1);
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

  .none {
    padding: var(--space-4);
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .add {
    display: flex;
    align-items: center;
    justify-content: center;
    gap: var(--space-2);
    width: 100%;
    min-height: var(--control-md);
    padding: var(--space-3) var(--space-4);
    border: none;
    /* Only the bottom corners, so the row sits inside the enclosure rather than
       on top of it. */
    border-end-start-radius: var(--radius-lg);
    border-end-end-radius: var(--radius-lg);
    background: none;
    color: var(--text-muted);
    font: inherit;
    font-size: var(--text-sm);
    font-weight: var(--weight-medium);
    cursor: pointer;
    transition:
      background-color var(--duration-fast) var(--ease-out),
      color var(--duration-fast) var(--ease-out);
  }

  .add:hover {
    background: var(--surface-hover);
    color: var(--text);
  }

  .plus {
    display: block;
    width: var(--space-4);
    height: var(--space-4);
  }

  .plus :global(svg) {
    display: block;
    width: 100%;
    height: 100%;
  }
</style>
