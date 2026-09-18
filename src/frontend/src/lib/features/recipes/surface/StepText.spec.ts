import { render } from '@testing-library/svelte';
import { describe, expect, it } from 'vitest';

import StepText from './StepText.svelte';
import type { Scaling } from './scaled.svelte';
import type { Step } from '../types';

/**
 * The step's text is rendered with `white-space: pre-wrap`, so every space and
 * newline that reaches the DOM is a space and a newline the cook sees. That
 * makes the whitespace in the component's own markup significant — a line
 * break written for readability between two expressions would show up as a
 * line break on the page — which is not the sort of thing anybody notices in
 * review. Hence these.
 */
const scaling = {
  show: (quantity: { value: number | null; unit: string | null }) => ({
    text: `${quantity.value ?? ''} ${quantity.unit ?? ''}`.trim()
  })
} as unknown as Scaling;

const butter = {
  kind: 'ingredient' as const,
  ingredientId: 'i-butter',
  name: 'butter',
  quantity: { value: 200, unit: 'g' }
};

const step = (segments: Step['segments']): Step =>
  ({ id: 's1', segments, uses: [], durationSeconds: null }) as Step;

describe('StepText', () => {
  it('keeps the line breaks the step was written with', () => {
    const { container } = render(StepText, {
      props: { step: step([{ kind: 'text', text: 'Bake.\nRest.\n\nSlice.' }]), scaling }
    });

    // Three lines because somebody meant three lines. An imported Tandoor step
    // arrives this way too: its instruction is Markdown rendered with a line
    // break per newline.
    expect(container.querySelector('p')?.textContent).toBe('Bake.\nRest.\n\nSlice.');
  });

  it.each([true, false])(
    'puts exactly one space inside an ingredient reference (interactive: %s)',
    (interactive) => {
      const { container } = render(StepText, {
        props: { step: step([butter]), scaling, interactive }
      });

      // Not "200 g\n        butter", which is what the markup would produce if
      // the amount and the name were written on separate lines.
      expect(container.querySelector('p')?.textContent).toBe('200 g butter');
    }
  );

  it('adds no whitespace of its own between the segments', () => {
    const { container } = render(StepText, {
      props: {
        step: step([{ kind: 'text', text: 'Melt ' }, butter, { kind: 'text', text: ' in.' }]),
        scaling
      }
    });

    expect(container.querySelector('p')?.textContent).toBe('Melt 200 g butter in.');
  });
});
