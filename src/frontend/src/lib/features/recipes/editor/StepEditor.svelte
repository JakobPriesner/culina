<script lang="ts">
  import { Button, IconButton } from '$ds';

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

  function setUses(index: number, uses: string[]) {
    onchange(steps.map((step, candidate) => (candidate === index ? { ...step, uses } : step)));
  }

  function add() {
    onchange([...steps, { id: null, segments: [], uses: [], durationSeconds: null }]);
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
  <ol class="list">
    {#each steps as step, index (step.id ?? index)}
      <li class="step">
        <div class="controls">
          <span class="number">{m['editor.stepLabel']({ number: index + 1 })}</span>

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

  <Button onclick={add}>{m['editor.addStep']()}</Button>
</div>

<style>
  .editor {
    display: flex;
    flex-direction: column;
    align-items: flex-start;
    gap: var(--space-4);
  }

  .list {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
    width: 100%;
    margin: 0;
    padding: 0;
    list-style: none;
  }

  .controls {
    display: flex;
    align-items: center;
    gap: var(--space-1);
    margin-bottom: var(--space-1);
  }

  .number {
    flex: 1;
    color: var(--text-subtle);
    font-size: var(--text-xs);
    letter-spacing: 0.08em;
    text-transform: uppercase;
  }
</style>
