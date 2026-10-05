import 'fake-indexeddb/auto';
import { fireEvent, waitFor, within } from '@testing-library/svelte';
import { tick } from 'svelte';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import type { RecipeReading } from '$features/recipes/types';
import { createCookingPip } from './pip.svelte';
import { cooking } from './stores/cooking.svelte';
import { kitchenTimers as timers } from './kitchen.svelte';
import { forgetKitchen } from './timerState';

vi.mock('./timerNotification', () => ({
  notifyTimerDone: vi.fn(),
  closeTimerNotification: vi.fn(),
  requestTimerNotificationPermission: vi.fn()
}));
vi.mock('./kitchenAudio', () => ({ playKitchenChime: vi.fn(), unlockAudio: vi.fn() }));

const recipe: RecipeReading = {
  id: 'r1',
  title: 'Butter sauce',
  description: null,
  language: 'en',
  yieldAmount: 2,
  yieldKind: 'servings',
  yieldLabel: null,
  prepMinutes: null,
  cookMinutes: null,
  totalMinutes: null,
  imageId: null,
  groups: [],
  tags: [],
  sourceUrl: null,
  updatedAt: '',
  steps: [
    {
      id: 'step-1',
      title: 'Simmer',
      uses: [],
      durationSeconds: 120,
      segments: [
        { kind: 'text', text: 'Melt ' },
        {
          kind: 'ingredient',
          ingredientId: 'butter',
          name: 'butter',
          quantity: { value: 200, unit: 'g' }
        }
      ]
    },
    {
      id: 'step-2',
      title: 'Serve',
      uses: [],
      durationSeconds: null,
      segments: [{ kind: 'text', text: 'Serve warm.' }]
    }
  ]
};
let pip: ReturnType<typeof createCookingPip>;
let frame: HTMLIFrameElement;
let child: Window;
let requestWindow: ReturnType<typeof vi.fn>;

beforeEach(async () => {
  cooking.reset();
  timers.clear();
  await forgetKitchen();
  vi.stubGlobal(
    'fetch',
    vi.fn(async (request: Request) => {
      const body = request.method === 'PATCH' ? await request.json() : {};
      return new Response(
        JSON.stringify({
          sessionId: 'pip-session',
          recipeId: recipe.id,
          recipeTitle: recipe.title,
          servings: 4,
          currentStepIndex: 0,
          startedAt: '',
          lastActiveAt: '',
          version: 1,
          ...body
        }),
        { headers: { 'Content-Type': 'application/json' } }
      );
    })
  );
  await cooking.resume();
  timers.load();
  await timers.refresh();
  frame = document.createElement('iframe');
  document.body.append(frame);
  child = frame.contentWindow!;
  vi.spyOn(child, 'close').mockImplementation(() => {});
  vi.spyOn(window, 'focus').mockImplementation(() => {});
  requestWindow = vi.fn(async () => child);
  Object.defineProperty(window, 'documentPictureInPicture', {
    configurable: true,
    value: { requestWindow }
  });
  pip = createCookingPip();
  document.documentElement.dataset['theme'] = 'warm-paper';
});
afterEach(async () => {
  pip.close();
  await tick();
  frame.remove();
  timers.clear();
  cooking.reset();
  Reflect.deleteProperty(window, 'documentPictureInPicture');
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

it('uses the same steps, scaled ingredients and timer actions in both windows', async () => {
  expect(await pip.open(recipe, vi.fn())).toBe(true);
  const view = within(child.document.body);
  await waitFor(() => expect(view.getByText('400 g butter')).toBeDefined());
  await fireEvent.click(view.getByRole('button', { name: 'Start 2 min timer' }));
  await waitFor(() => expect(timers.timers).toHaveLength(1));
  await fireEvent.click(view.getByRole('button', { name: 'Pause timer' }));
  expect(timers.timers[0]?.pausedRemaining).toBeGreaterThan(0);
  expect(timers.runningCount).toBe(0);
  timers.resume(0);
  await waitFor(() => expect(view.getByRole('button', { name: 'Pause timer' })).toBeDefined());
  await fireEvent.click(view.getByRole('button', { name: 'Next step' }));
  expect(cooking.session?.currentStepIndex).toBe(1);
  cooking.moveTo(recipe.id, 0);
  await waitFor(() => expect(view.getByText('Step 1 of 2')).toBeDefined());
  pip.close();
  expect(cooking.session?.currentStepIndex).toBe(0);
  expect(timers.timers).toHaveLength(1);
});
it('cleans up a browser-closed window and can reopen without resetting the cook', async () => {
  await pip.open(recipe, vi.fn());
  cooking.moveTo(recipe.id, 1);
  child.dispatchEvent(new Event('pagehide'));
  expect(pip.active).toBe(false);
  expect(pip.sessionId).toBeNull();
  expect(child.document.body.children).toHaveLength(0);
  expect(cooking.session?.currentStepIndex).toBe(1);
  await pip.open(recipe, vi.fn());
  await waitFor(() => expect(within(child.document.body).getByText('Serve warm.')).toBeDefined());
});
it('does not open after a session ends while the browser is still answering', async () => {
  let answer!: (value: Window) => void;
  requestWindow.mockReturnValue(
    new Promise<Window>((resolve) => {
      answer = resolve;
    })
  );
  const pending = pip.open(recipe, vi.fn());
  pip.close();
  answer(child);
  expect(await pending).toBe(false);
  expect(pip.active).toBe(false);
  expect(child.close).toHaveBeenCalled();
});
it('recovers from browser refusal and hides support when the API is absent', async () => {
  requestWindow.mockRejectedValue(new Error('NotAllowedError'));
  expect(await pip.open(recipe, vi.fn())).toBe(false);
  expect(pip.opening).toBe(false);
  Reflect.deleteProperty(window, 'documentPictureInPicture');
  expect(pip.supported).toBe(false);
  expect(await pip.open(recipe, vi.fn())).toBe(false);
});
it('returns to the existing finishing flow instead of silently recording a cook', async () => {
  const returnToCooking = vi.fn();
  await pip.open(recipe, returnToCooking);
  cooking.moveTo(recipe.id, 1);
  await tick();
  await fireEvent.click(
    within(child.document.body).getByRole('button', { name: 'Back to finish' })
  );
  expect(returnToCooking).toHaveBeenCalledOnce();
  expect(pip.active).toBe(false);
  expect(cooking.session).not.toBeNull();
});
