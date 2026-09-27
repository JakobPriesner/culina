import { describe, expect, it } from 'vitest';

import { toWireSteps } from './mappers';
import type { Step } from './types';

const step = (segments: Step['segments']): Step => ({
  id: null,
  title: null,
  segments,
  uses: [],
  durationSeconds: null
});

describe('steps on their way to the server', () => {
  it('leaves out a step nobody has written in yet', () => {
    const written = step([{ kind: 'text', text: 'Melt the butter.' }]);

    expect(toWireSteps([written, step([]), step([{ kind: 'text', text: '' }])])).toEqual([
      {
        stepId: undefined,
        title: undefined,
        durationSeconds: undefined,
        uses: [],
        segments: [{ type: 'text', value: 'Melt the butter.' }]
      }
    ]);
  });

  it('keeps a step that is only a mention', () => {
    const mention = step([
      {
        kind: 'ingredient',
        ingredientId: 'i-butter',
        name: 'butter',
        quantity: { value: 200, unit: 'g' }
      }
    ]);

    expect(toWireSteps([mention])).toHaveLength(1);
  });
});
