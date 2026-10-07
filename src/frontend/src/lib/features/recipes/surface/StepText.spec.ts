import { render } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';

import StepText from './StepText.svelte';
import type { Scaling } from './scaled.svelte';
import type { Step } from '../types';

/** The step text is `white-space: pre-wrap`, so whitespace in the component's markup is significant; these catch accidental line breaks. */
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
  ({ id: 's1', title: null, segments, uses: [], durationSeconds: null }) as Step;

describe('StepText', () => {
  it('keeps the line breaks the step was written with', () => {
    const { container } = render(StepText, {
      props: { step: step([{ kind: 'text', text: 'Bake.\nRest.\n\nSlice.' }]), scaling }
    });

    // Two lines in one paragraph, a blank line starts the next; imported Tandoor steps arrive this way.
    const paragraphs = [...container.querySelectorAll('p')].map((one) => one.textContent);

    expect(paragraphs).toEqual(['Bake.\nRest.', 'Slice.']);
  });

  it.each([true, false])(
    'puts exactly one space inside an ingredient reference (interactive: %s)',
    (interactive) => {
      const { container } = render(StepText, {
        props: { step: step([butter]), scaling, interactive }
      });

      expect(container.querySelector('p')?.textContent).toBe('200 g butter');
    }
  );

  it.each([true, false])(
    'puts no space before an ingredient that has no amount (interactive: %s)',
    (interactive) => {
      const salt = {
        ...butter,
        ingredientId: 'i-salt',
        name: 'salt',
        quantity: { value: null, unit: null }
      };
      const { container } = render(StepText, {
        props: {
          step: step([{ kind: 'text', text: 'a pinch of ' }, salt]),
          scaling,
          interactive
        }
      });

      expect(container.querySelector('p')?.textContent).toBe('a pinch of salt');
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

  it('renders the step as Markdown, references and all', () => {
    const { container } = render(StepText, {
      props: {
        step: step([
          { kind: 'text', text: 'Melt **' },
          butter,
          { kind: 'text', text: '** slowly.' }
        ]),
        scaling
      }
    });

    expect(container.querySelector('strong button')?.textContent).toBe('200 g butter');
    expect(container.querySelector('p')?.textContent).toBe('Melt 200 g butter slowly.');
  });

  it('sets a list as a list', () => {
    const { container } = render(StepText, {
      props: { step: step([{ kind: 'text', text: '- salt\n- pepper' }]), scaling }
    });

    expect([...container.querySelectorAll('ul li')].map((one) => one.textContent)).toEqual([
      'salt',
      'pepper'
    ]);
  });

  it('leaves a link inert where the whole step is the control', () => {
    const markdown = { kind: 'text' as const, text: 'see [the source](https://example.com)' };

    const live = render(StepText, { props: { step: step([markdown]), scaling } });

    expect(live.container.querySelector('a')?.getAttribute('href')).toBe('https://example.com');

    const cooking = render(StepText, {
      props: { step: step([markdown]), scaling, interactive: false }
    });

    expect(cooking.container.querySelector('a')).toBeNull();
    expect(cooking.container.querySelector('button')).toBeNull();
    expect(cooking.container.querySelector('p')?.textContent).toBe('see the source');
  });

  it('illuminates the ingredient token when highlighted by the ingredient list', () => {
    const { container } = render(StepText, {
      props: { step: step([butter]), scaling, highlighted: 'i-butter' }
    });

    const button = container.querySelector('button.ingredient');
    expect(button).toHaveClass('is-highlighted');
  });

  it('opens an Apple-style Quick-Look card on click', async () => {
    const { container } = render(StepText, {
      props: { step: step([butter]), scaling }
    });

    const button = container.querySelector('button.ingredient')!;
    expect(container.querySelector('.quick-look')).toBeNull();

    await userEvent.click(button);

    expect(container.querySelector('.quick-look')).toBeInTheDocument();
    expect(container.querySelector('.quick-look .name')?.textContent).toBe('butter');
  });
});
