<script lang="ts">
  import { tick } from 'svelte';

  import { m } from '$shell/i18n';
  import { moved } from './stepEdits';
  import StepRow from './StepRow.svelte';
  import type { Ingredient, Step } from '../types';

  /**
   * Steps as sentences ("Melt @butter in the pan"); an unknown name is added to the list from the sentence.
   * Reordering is buttons, not drag, for keyboard use; the number above a step is its editable name.
   */
  interface Props {
    steps: readonly Step[];
    /** What a mention can point at, and what a new one is added to. */
    ingredients: readonly Ingredient[];
    onchange: (steps: Step[]) => void;
    onaddingredient: (name: string) => void;
  }

  let { steps, ingredients, onchange, onaddingredient }: Props = $props();

  /** Adds a step and focuses it, after a tick because the field doesn't exist until the list renders. */
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

  /* Same enclosure as the ingredients: steps and the add row are one thing. */
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
    /* Only the bottom corners, so the row sits inside the enclosure. */
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
