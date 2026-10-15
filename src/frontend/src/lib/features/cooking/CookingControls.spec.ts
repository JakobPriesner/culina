import { screen, within } from '@testing-library/svelte';
import { userEvent } from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';

import type { Recipe } from '$features/recipes/types';
import CookingControls from './CookingControls.svelte';
import { kitchenTimers } from './kitchen.svelte';
import { renderWithProviders } from '$lib/test/render';

/* A wait the method only writes down ("4 Min. braten") is a timer one tap away, not one to set by hand at the hob. */
const recipe = (text: string, durationSeconds: number | null = null) =>
  ({
    id: 'r1',
    title: 'Chili',
    steps: [
      { id: 's1', title: null, segments: [{ kind: 'text', text }], uses: [], durationSeconds }
    ]
  }) as unknown as Recipe;

const show = (cooked: Recipe) =>
  renderWithProviders(CookingControls, {
    props: {
      recipe: cooked,
      currentStep: 0,
      ready: true,
      recipeHref: '/recipes/r1',
      autoScrolling: false,
      onmove: () => {},
      onadvance: () => {},
      onautoscroll: () => {},
      onnotes: () => {},
      onkitchen: () => {}
    }
  });

afterEach(() => vi.restoreAllMocks());

describe('timers for the waits a step writes down', () => {
  it('offers each wait in the step, a range from its start', async () => {
    const start = vi.spyOn(kitchenTimers, 'start').mockImplementation(() => {});
    show(recipe('5–10 Min. anbraten, 4 Min. braten, dann 11⁄2 Std. garen.'));

    const group = screen.getByRole('group', { name: 'Timer' });
    const offered = within(group).getAllByRole('button');
    expect(offered.map((button) => button.textContent?.trim())).toEqual([
      '5 min',
      '4 min',
      '90 min'
    ]);

    await userEvent.click(within(group).getByRole('button', { name: 'Start 90 min timer' }));
    expect(start).toHaveBeenCalledWith(0, 5400, 'Step 1');
  });

  it('keeps the timer the recipe set for the step instead', () => {
    show(recipe('4 Min. braten', 600));

    expect(screen.queryByRole('group', { name: 'Timer' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Start 10 min timer' })).toBeInTheDocument();
  });

  it('takes back a timer started by mistake', async () => {
    vi.spyOn(kitchenTimers, 'timers', 'get').mockReturnValue([
      { stepIndex: 0, label: 'Step 1', endsAt: Date.now() + 240_000 }
    ]);
    vi.spyOn(kitchenTimers, 'remaining').mockReturnValue(240);
    const dismiss = vi.spyOn(kitchenTimers, 'dismiss').mockImplementation(() => {});
    show(recipe('4 Min. braten'));

    expect(screen.queryByRole('group', { name: 'Timer' })).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Cancel timer' }));
    expect(dismiss).toHaveBeenCalledWith(0);
  });

  it('offers nothing for a step that names no wait', () => {
    show(recipe('Koriander grob hacken.'));

    expect(screen.queryByRole('group', { name: 'Timer' })).not.toBeInTheDocument();
  });
});
