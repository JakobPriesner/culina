import { render, screen } from '@testing-library/svelte';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { olliSetting } from '$shell/olli/setting.svelte';

import StepScene from './StepScene.svelte';

/* The step writes itself, then a pointer presses "+" twice and every amount follows. */
afterEach(() => {
  vi.useRealTimers();
  olliSetting.reset();
});

describe('the step scene', () => {
  it('shows more servings changing every amount, then hands over', async () => {
    vi.useFakeTimers();
    const onreact = vi.fn();

    render(StepScene, { props: { onreact } });

    await vi.advanceTimersByTimeAsync(15_000);

    expect(screen.getByText('Ingredients for 4 servings:')).toBeInTheDocument();
    expect(screen.getAllByText('40 g')).toHaveLength(2);
    expect(screen.getByText('×2')).toBeInTheDocument();
    expect(onreact).toHaveBeenLastCalledWith('celebrating', expect.stringMatching(/Your turn!/));
  });

  it('asks the cook to try it where nothing moves by itself', () => {
    olliSetting.animate(false);
    const onreact = vi.fn();

    render(StepScene, { props: { onreact } });

    expect(screen.getByText('Ingredients for 2 servings:')).toBeInTheDocument();
    expect(onreact).toHaveBeenLastCalledWith('idea', expect.stringMatching(/Try the servings!/));
  });
});
