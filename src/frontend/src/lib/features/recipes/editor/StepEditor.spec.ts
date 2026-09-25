import { render, screen } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import StepEditor from './StepEditor.svelte';
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
});
