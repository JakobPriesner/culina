<script lang="ts">
  import { tick } from 'svelte';

  import { m } from '$shell/i18n';
  import { moved } from './stepEdits';
  import StepRow from './StepRow.svelte';
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
</script>

<div class="editor">
  <div class="panel">
    {#if steps.length > 0}
      <ol class="list">
        {#each steps as step, index (step.id ?? index)}
          <StepRow
            {step}
            {index}
            count={steps.length}
            {ingredients}
            onchange={(changed) => onchange(steps.map((one, at) => (at === index ? changed : one)))}
            onmove={(by) => onchange([...moved(steps, index, by)])}
            onremove={() => remove(index)}
            {onaddingredient}
          />
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

  .list > :global(.step + .step),
  .add {
    border-top: 1px solid var(--border);
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
