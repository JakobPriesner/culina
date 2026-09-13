<script lang="ts">
  import { Button, IconButton, TextArea } from '$ds';

  import { m } from '$shell/i18n';
  import type { Ingredient, Step, StepSegment } from '../types';

  /**
   * The steps, as plain text.
   *
   * An author writes "Melt the butter in the pan" — they do not place tokens.
   * The link to an ingredient is found by matching the words they already
   * typed, so the payoff (a step that scales with the list) costs the author
   * nothing.
   *
   * Reordering is buttons, not drag. Drag alone cannot be done with a keyboard,
   * and a recipe is rearranged rarely enough that two arrows are no hardship.
   */
  interface Props {
    steps: readonly Step[];
    /** Used to find references in the text the author typed. */
    ingredients: readonly Ingredient[];
    onchange: (steps: Step[]) => void;
  }

  let { steps, ingredients, onchange }: Props = $props();

  /** The plain sentence a step's segments spell out. */
  const textOf = (step: Step): string =>
    step.segments
      .map((segment) => (segment.kind === 'text' ? segment.text : segment.name))
      .join('');

  /**
   * Splits a sentence around the ingredient names it mentions.
   *
   * Longest first, so "olive oil" wins over "oil". Only ingredients that have
   * an id — one the server has already assigned — can be referenced; a line
   * typed a moment ago is still just words until it has been saved.
   */
  function toSegments(text: string): StepSegment[] {
    const named = ingredients
      .filter((one) => one.id && one.name)
      .sort((a, b) => b.name.length - a.name.length);

    const segments: StepSegment[] = [];
    let rest = text;

    outer: while (rest.length > 0) {
      for (const ingredient of named) {
        const at = rest.toLowerCase().indexOf(ingredient.name.toLowerCase());

        if (at !== -1) {
          if (at > 0) {
            segments.push({ kind: 'text', text: rest.slice(0, at) });
          }

          segments.push({
            kind: 'ingredient',
            ingredientId: ingredient.id,
            name: ingredient.name,
            quantity: ingredient.quantity
          });

          rest = rest.slice(at + ingredient.name.length);

          continue outer;
        }
      }

      segments.push({ kind: 'text', text: rest });

      break;
    }

    return segments;
  }

  function update(index: number, text: string) {
    onchange(
      steps.map((step, candidate) =>
        candidate === index ? { ...step, segments: toSegments(text) } : step
      )
    );
  }

  function add() {
    onchange([...steps, { id: null, segments: [], durationSeconds: null }]);
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

        <TextArea
          id="step-{index}"
          label={m['editor.stepLabel']({ number: index + 1 })}
          value={textOf(step)}
          rows={2}
          oninput={(text) => update(index, text)}
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
