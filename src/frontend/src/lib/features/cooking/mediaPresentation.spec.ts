import { describe, expect, it } from 'vitest';

import type { RecipeReading } from '$features/recipes/types';

import { selectTimer, stepExcerpt } from './mediaPresentation';
import type { KitchenTimer } from './timerState';

const now = 1_000_000;
const running = (stepIndex: number, secondsLeft: number): KitchenTimer => ({
  stepIndex,
  label: `step ${stepIndex}`,
  endsAt: now + secondsLeft * 1000
});
const paused = (stepIndex: number, pausedRemaining: number): KitchenTimer => ({
  stepIndex,
  label: `step ${stepIndex}`,
  endsAt: now,
  pausedRemaining
});

describe('selectTimer', () => {
  it('prefers the timer on the step being cooked', () => {
    expect(selectTimer([running(0, 10), running(2, 500)], 2, null, now)?.stepIndex).toBe(2);
  });

  it('then the timer just paused from the remote', () => {
    expect(selectTimer([running(0, 10), paused(3, 60)], 1, 3, now)?.stepIndex).toBe(3);
  });

  it('then the running timer that rings first', () => {
    expect(selectTimer([running(0, 300), running(1, 20)], 5, null, now)?.stepIndex).toBe(1);
  });

  it('then the earliest step among paused ones', () => {
    expect(selectTimer([paused(4, 60), paused(2, 60)], 5, null, now)?.stepIndex).toBe(2);
  });

  it('never offers a timer that has finished', () => {
    expect(selectTimer([running(0, -5), paused(1, 0)], 0, null, now)).toBeUndefined();
  });
});

describe('stepExcerpt', () => {
  const step: RecipeReading['steps'][number] = {
    id: 's1',
    title: null,
    uses: [],
    durationSeconds: null,
    segments: [
      { kind: 'text', text: '**Melt**\n\n' },
      {
        kind: 'ingredient',
        ingredientId: 'b',
        name: 'butter',
        quantity: { value: 200, unit: 'g' }
      },
      { kind: 'text', text: ' and [stir](https://example.com).' }
    ]
  };

  it('flattens formatting, links and ingredients into one line', () => {
    expect(stepExcerpt(step, () => ({ text: '200 g' }))).toBe('Melt 200 g butter and stir.');
  });

  it('is empty without a step', () => {
    expect(stepExcerpt(undefined, () => ({ text: '' }))).toBe('');
  });

  it('is cut to what a lock screen can show', () => {
    const long = { ...step, segments: [{ kind: 'text' as const, text: 'word '.repeat(100) }] };

    expect(stepExcerpt(long, () => ({ text: '' }))).toHaveLength(180);
  });
});
