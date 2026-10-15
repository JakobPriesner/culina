import { screen, waitFor } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import CookPage from './+page.svelte';
import { clientError, ErrorCodes } from '$api';
import { session } from '$features/auth/session.svelte';
import { cooking } from '$features/cooking/stores/cooking.svelte';
import { kitchenTimers } from '$features/cooking/kitchen.svelte';
import { speech } from '$features/cooking/speech.svelte';
import { recipes } from '$features/recipes/stores/recipes.svelte';
import type { Recipe } from '$features/recipes/types';
import { renderWithProviders } from '$lib/test/render';

vi.mock('$app/state', () => ({
  page: { params: { recipeId: 'recipe-1' }, url: new URL('http://localhost/recipes/recipe-1/cook') }
}));

const step = (id: string) => ({
  id,
  title: null,
  segments: [],
  uses: [],
  durationSeconds: null
});

const recipe: Recipe = {
  id: 'recipe-1',
  householdId: 'household-1',
  title: 'Risotto',
  description: null,
  language: 'en',
  yieldAmount: 4,
  yieldKind: 'servings',
  yieldLabel: null,
  prepMinutes: null,
  cookMinutes: null,
  totalMinutes: null,
  imageId: null,
  groups: [],
  steps: [step('s-1'), step('s-2')],
  tags: [],
  sourceUrl: null,
  createdBy: 'user-1',
  createdAt: '2026-09-20T12:00:00Z',
  updatedAt: '2026-09-20T12:00:00Z',
  version: 1
};

beforeEach(() => {
  vi.stubGlobal(
    'IntersectionObserver',
    class {
      observe() {}
      disconnect() {}
    }
  );
  vi.spyOn(session, 'activeHouseholdId', 'get').mockReturnValue('household-1');
  vi.spyOn(recipes, 'detail', 'get').mockReturnValue(recipe);
  vi.spyOn(recipes, 'detailStatus', 'get').mockReturnValue('ready');
  vi.spyOn(recipes, 'load').mockResolvedValue();
  vi.spyOn(cooking, 'resolved', 'get').mockReturnValue(true);
  vi.spyOn(kitchenTimers, 'load').mockResolvedValue();
});

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

describe('a cook session that could not be started', () => {
  it('says so and starts again on retry', async () => {
    const start = vi
      .spyOn(cooking, 'start')
      .mockResolvedValueOnce(clientError(ErrorCodes.offline, 'Offline.'))
      .mockResolvedValue(null);

    renderWithProviders(CookPage);

    const alert = await screen.findByRole('alert');

    expect(alert.textContent).toContain('could not be started');

    await userEvent.click(screen.getByRole('button', { name: 'Try again' }));

    await waitFor(() => expect(screen.queryByRole('alert')).toBeNull());
    expect(start).toHaveBeenCalledTimes(2);
  });

  it('shows nothing when the session starts', async () => {
    vi.spyOn(cooking, 'start').mockResolvedValue(null);

    renderWithProviders(CookPage);

    await waitFor(() => expect(cooking.start).toHaveBeenCalled());
    expect(screen.queryByRole('alert')).toBeNull();
  });
});

describe('reading a step aloud', () => {
  const spoken: Recipe = {
    ...recipe,
    steps: [
      {
        ...step('s-1'),
        segments: [{ kind: 'text', text: 'Stir in 2 tbsp butter and ½ tsp salt.' }]
      },
      { ...step('s-2'), segments: [{ kind: 'text', text: 'Serve at 60°C.' }] }
    ]
  };

  let said: string[];
  let cancelled: number;

  const engine = () => ({
    speak: vi.fn((utterance: { text: string }) => void said.push(utterance.text)),
    cancel: vi.fn(() => void (cancelled += 1)),
    getVoices: () => []
  });

  async function cookStep(index: number) {
    vi.stubGlobal(
      'fetch',
      vi.fn(() =>
        Promise.resolve(
          new Response(
            JSON.stringify({
              sessionId: 'session-1',
              recipeId: 'recipe-1',
              recipeTitle: 'Risotto',
              servings: 4,
              currentStepIndex: index,
              startedAt: '2026-09-20T12:00:00Z',
              lastActiveAt: '2026-09-20T12:00:00Z',
              version: 1
            }),
            { headers: { 'Content-Type': 'application/json' } }
          )
        )
      )
    );
    await cooking.resume();
  }

  beforeEach(() => {
    said = [];
    cancelled = 0;
    localStorage.clear();
    speech.cancel();
    speech.setAutoRead(false);
    vi.stubGlobal(
      'SpeechSynthesisUtterance',
      class {
        lang = '';
        voice = null;
        constructor(readonly text: string) {}
      }
    );
    vi.stubGlobal('speechSynthesis', engine());
    vi.spyOn(recipes, 'detail', 'get').mockReturnValue(spoken);
    vi.spyOn(cooking, 'start').mockResolvedValue(null);
  });

  afterEach(() => {
    cooking.reset();
    speech.cancel();
  });

  it('says the current step in words when the speaker is pressed, and stops on a second press', async () => {
    await cookStep(0);
    renderWithProviders(CookPage);

    await userEvent.click(await screen.findByRole('button', { name: 'Read step aloud' }));

    expect(said).toEqual(['Stir in 2 tablespoons butter and one half teaspoon salt.']);

    await userEvent.click(screen.getByRole('button', { name: 'Stop reading' }));

    expect(screen.getByRole('button', { name: 'Read step aloud' })).toBeTruthy();
  });

  it('stops reading when the step is changed by hand, and does not start the next', async () => {
    await cookStep(0);
    renderWithProviders(CookPage);

    await userEvent.click(await screen.findByRole('button', { name: 'Read step aloud' }));
    await userEvent.click(screen.getByRole('button', { name: 'Next step' }));

    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Read step aloud' })).toBeTruthy()
    );
    expect(said).toHaveLength(1);
  });

  it('reads each new step when auto-read is on, but not the one it opens at', async () => {
    speech.setAutoRead(true);
    await cookStep(0);
    renderWithProviders(CookPage);

    await screen.findByRole('button', { name: 'Read step aloud' });
    expect(said).toEqual([]);

    await userEvent.click(screen.getByRole('button', { name: 'Next step' }));

    await waitFor(() => expect(said).toEqual(['Serve at 60 degrees Celsius.']));
  });

  it('gives way to a timer alarm', async () => {
    await cookStep(0);
    renderWithProviders(CookPage);

    await userEvent.click(await screen.findByRole('button', { name: 'Read step aloud' }));
    speech.interrupt();

    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Read step aloud' })).toBeTruthy()
    );
  });

  it('offers no speaker where the browser cannot speak', async () => {
    vi.stubGlobal('speechSynthesis', undefined);
    await cookStep(0);
    renderWithProviders(CookPage);

    await screen.findByRole('button', { name: 'Next step' });
    expect(screen.queryByRole('button', { name: 'Read step aloud' })).toBeNull();
  });
});
