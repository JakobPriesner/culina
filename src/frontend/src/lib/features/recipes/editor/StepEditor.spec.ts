import { render, screen } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import StepEditor from './StepEditor.svelte';
import { LONGEST_COOKING_STEP } from './stepEdits';
import type { Step } from '../types';

const step: Step = {
  id: 's1',
  title: null,
  segments: [{ kind: 'text', text: 'Let the dough rise.' }],
  uses: [],
  durationSeconds: null
};

describe('the step editor', () => {
  it('writes a timer duration in seconds for cook mode', async () => {
    const onchange = vi.fn();

    render(StepEditor, {
      steps: [step],
      ingredients: [],
      onchange,
      onaddingredient: () => {}
    });

    await userEvent.type(screen.getByRole('spinbutton', { name: /Timer for step 1/ }), '12.5');

    expect(onchange).toHaveBeenLastCalledWith([{ ...step, durationSeconds: 750 }]);
  });

  it('warns about a step too long for one screen without blocking edits', () => {
    render(StepEditor, {
      steps: [
        { ...step, segments: [{ kind: 'text', text: 'x'.repeat(LONGEST_COOKING_STEP + 1) }] }
      ],
      ingredients: [],
      onchange: () => {},
      onaddingredient: () => {}
    });

    expect(screen.getByText(/may not fit on one screen/)).toBeInTheDocument();
  });

  it('stays quiet for a step that fits', () => {
    render(StepEditor, {
      steps: [step],
      ingredients: [],
      onchange: () => {},
      onaddingredient: () => {}
    });

    expect(screen.queryByText(/may not fit on one screen/)).not.toBeInTheDocument();
  });

  it('links every ingredient the steps name in one tap, as a mention where the words spell it', async () => {
    const onchange = vi.fn();
    const quantity = { value: 250, unit: 'g' };
    const ingredients = [
      { id: 'pasta', name: 'Spaghetti', note: null, quantity },
      { id: 'tomatoes', name: 'Dose geschälte Tomaten', note: null, quantity }
    ];
    const text = (id: string, words: string): Step => ({
      ...step,
      id,
      segments: [{ kind: 'text', text: words }]
    });

    render(StepEditor, {
      steps: [text('s1', 'Spaghetti kochen.'), text('s2', 'Die Tomaten köcheln.')],
      ingredients,
      onchange,
      onaddingredient: () => {}
    });

    await userEvent.click(screen.getByRole('button', { name: 'Link 2 spotted ingredients' }));

    const [linked] = onchange.mock.lastCall as [Step[]];

    expect(linked[0]!.segments.some((one) => one.kind === 'ingredient')).toBe(true);
    expect(linked[1]!.uses).toEqual(['tomatoes']);
  });

  it('says how to mention once, on the first step, not on every card', () => {
    render(StepEditor, {
      steps: [step, { ...step, id: 's2' }],
      ingredients: [],
      onchange: () => {},
      onaddingredient: () => {}
    });

    const hints = screen.getAllByText(/Type @ to/);

    expect(hints.filter((hint) => !hint.classList.contains('quiet'))).toHaveLength(1);
  });
});
