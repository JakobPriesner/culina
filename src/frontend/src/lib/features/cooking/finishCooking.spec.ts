import { afterEach, describe, expect, it, vi } from 'vitest';

import { finishCooking } from './finishCooking';
import { cookLog } from './stores/cookLog.svelte';
import { cooking } from './stores/cooking.svelte';

/* "I made it" for one dish of a meal must not take the other dish off the hob. */
afterEach(() => {
  cooking.reset();
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

describe('finishing a recipe', () => {
  it('leaves another recipe’s session cooking', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(() =>
        Promise.resolve(
          new Response(
            JSON.stringify({
              sessionId: 's1',
              recipeId: 'main',
              recipeTitle: 'Main',
              servings: 2,
              currentStepIndex: 3,
              startedAt: '2026-10-17T18:00:00Z',
              lastActiveAt: '2026-10-17T18:10:00Z',
              version: 1
            })
          )
        )
      )
    );
    await cooking.start('main', 2);
    const end = vi.spyOn(cooking, 'end');
    vi.spyOn(cookLog, 'record').mockResolvedValue({ entryId: 'c1', count: 1 });

    const finished = await finishCooking('side', 2, null, true);

    expect(end).not.toHaveBeenCalled();
    expect(cooking.session?.recipeId).toBe('main');
    expect(finished.recorded?.entryId).toBe('c1');
  });
});
