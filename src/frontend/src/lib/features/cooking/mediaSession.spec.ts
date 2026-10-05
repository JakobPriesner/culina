import 'fake-indexeddb/auto';
import { fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { tick } from 'svelte';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import type { RecipeReading } from '$features/recipes/types';
import { cooking } from './stores/cooking.svelte';
import { kitchenTimers as timers } from './kitchen.svelte';
import { forgetKitchen } from './timerState';
import { kitchenMediaSession as remote } from './mediaSession.svelte';
import KitchenRuntime from './KitchenRuntime.svelte';
import CookingRemoteToggle from './CookingRemoteToggle.svelte';

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
  imageId: 'photo-1',
  groups: [],
  tags: [],
  sourceUrl: null,
  updatedAt: '',
  steps: [
    {
      id: 's1',
      title: 'Simmer',
      uses: [],
      durationSeconds: 120,
      segments: [
        { kind: 'text', text: '**Melt** ' },
        {
          kind: 'ingredient',
          ingredientId: 'butter',
          name: 'butter',
          quantity: { value: 200, unit: 'g' }
        },
        { kind: 'text', text: '. [Stir](https://example.com).' }
      ]
    },
    {
      id: 's2',
      title: 'Serve',
      uses: [],
      durationSeconds: null,
      segments: [{ kind: 'text', text: 'Serve warm.' }]
    }
  ]
};

class AudioStub extends EventTarget {
  loop = false;
  paused = true;
  constructor(readonly src: string) {
    super();
  }
  play = vi.fn(async () => {
    this.paused = false;
  });
  pause = vi.fn(() => {
    if (!this.paused) {
      this.paused = true;
      this.dispatchEvent(new Event('pause'));
    }
  });
  removeAttribute = vi.fn();
  load = vi.fn();
}
let audio: AudioStub;
let handlers: Map<MediaSessionAction, MediaSessionActionHandler | null>;
let media: {
  metadata: MediaMetadata | null;
  playbackState: MediaSessionPlaybackState;
  setActionHandler: ReturnType<typeof vi.fn>;
};
let metadataWrites = vi.fn<(value: MediaMetadataInit) => void>();
let fetchMock = vi.fn<(request: Request) => Promise<Response>>();
let clock: number;
const press = (action: MediaSessionAction) => handlers.get(action)?.({ action });

beforeEach(async () => {
  remote.stop();
  cooking.reset();
  timers.clear();
  await forgetKitchen();
  clock = 1800000000000;
  vi.spyOn(Date, 'now').mockImplementation(() => clock);
  handlers = new Map();
  metadataWrites = vi.fn();
  media = {
    metadata: null,
    playbackState: 'none',
    setActionHandler: vi.fn(
      (action: MediaSessionAction, handler: MediaSessionActionHandler | null) =>
        handlers.set(action, handler)
    )
  };
  vi.stubGlobal(
    'MediaMetadata',
    class {
      constructor(value: MediaMetadataInit) {
        metadataWrites(value);
        Object.assign(this, value);
      }
    }
  );
  Object.defineProperty(navigator, 'mediaSession', { configurable: true, value: media });
  vi.stubGlobal(
    'Audio',
    vi.fn(function (src: string) {
      audio = new AudioStub(src);
      return audio;
    })
  );
  fetchMock = vi.fn(
    async (request: Request) =>
      new Response(
        JSON.stringify({
          sessionId: 'media-session',
          recipeId: recipe.id,
          recipeTitle: recipe.title,
          servings: 4,
          currentStepIndex: 0,
          startedAt: '',
          lastActiveAt: '',
          version: 1,
          ...(request.method === 'PATCH' ? await request.json() : {})
        }),
        { headers: { 'Content-Type': 'application/json' } }
      )
  );
  vi.stubGlobal('fetch', fetchMock);
  await cooking.resume();
  timers.load();
  await timers.refresh();
});
afterEach(async () => {
  remote.stop();
  cooking.reset();
  timers.clear();
  await timers.refresh();
  Reflect.deleteProperty(navigator, 'mediaSession');
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

it('starts from the click and publishes the same scaled, readable instructions and artwork', async () => {
  const starting = remote.start(recipe);
  expect(audio.play).toHaveBeenCalledOnce();
  expect(audio.src).toContain('cooking-silence.wav');
  expect(audio.loop).toBe(true);
  expect(await starting).toBe(true);
  expect(media.metadata).toMatchObject({
    title: 'Butter sauce · Step 1 of 2',
    artist: 'Simmer · Melt 400 g butter. Stir.',
    artwork: [{ src: expect.stringContaining('/api/v1/recipes/r1/image?w=400&v=photo-1') }]
  });
  expect(media.playbackState).toBe('playing');
});

it('moves the shared session within bounds without recording a completed cook', async () => {
  await remote.start(recipe);
  press('previoustrack');
  expect(cooking.session?.currentStepIndex).toBe(0);
  press('nexttrack');
  expect(cooking.session?.currentStepIndex).toBe(1);
  expect(media.metadata).toMatchObject({ artist: 'Serve · Serve warm.' });
  press('nexttrack');
  expect(cooking.session?.currentStepIndex).toBe(1);
  expect(cooking.session).not.toBeNull();
  press('previoustrack');
  expect(cooking.session?.currentStepIndex).toBe(0);
});

it('pauses and resumes the current timer using its persisted deadline', async () => {
  timers.start(0, 120, 'Simmer');
  await remote.start(recipe);
  clock += 10000;
  press('pause');
  expect(timers.timers[0]?.pausedRemaining).toBe(110);
  expect(media.playbackState).toBe('paused');
  clock += 60000;
  press('play');
  await waitFor(() => expect(timers.timers[0]?.pausedRemaining).toBeUndefined());
  expect(timers.timers[0]?.endsAt).toBe(clock + 110000);
  expect(media.playbackState).toBe('playing');
  await timers.refresh();
  expect(timers.timers[0]?.endsAt).toBe(clock + 110000);
});

it('keeps Play paired with the fallback timer it paused, even with other running timers', async () => {
  timers.start(0, 60, 'First pan');
  timers.start(2, 120, 'Second pan');
  cooking.moveTo(recipe.id, 1);
  await remote.start(recipe);
  press('pause');
  expect(timers.timers.find((t) => t.stepIndex === 0)?.pausedRemaining).toBe(60);
  expect(timers.timers.find((t) => t.stepIndex === 2)?.pausedRemaining).toBeUndefined();
  press('play');
  await waitFor(() =>
    expect(timers.timers.find((t) => t.stepIndex === 0)?.pausedRemaining).toBeUndefined()
  );
});

it('ignores expired timers and handles play/pause without starting a recipe timer', async () => {
  timers.start(0, 1, 'Old timer');
  clock += 2000;
  await remote.start(recipe);
  press('pause');
  expect(timers.timers[0]?.pausedRemaining).toBeUndefined();
  expect(media.playbackState).toBe('paused');
  press('play');
  await waitFor(() => expect(media.playbackState).toBe('playing'));
  expect(timers.timers).toHaveLength(1);
});

it('throttles countdown metadata while step and pause changes are immediate', async () => {
  timers.start(0, 120, 'Simmer');
  await remote.start(recipe);
  expect(metadataWrites).toHaveBeenCalledTimes(1);
  for (let i = 0; i < 9; i++) {
    clock += 1000;
    remote.sync(null);
  }
  expect(metadataWrites).toHaveBeenCalledTimes(1);
  clock += 1000;
  remote.sync(null);
  expect(metadataWrites).toHaveBeenCalledTimes(2);
  press('pause');
  expect(metadataWrites).toHaveBeenCalledTimes(3);
  press('nexttrack');
  expect(metadataWrites).toHaveBeenCalledTimes(4);
});

it('keeps cooking metadata when browsing another recipe and updates shortened recipes safely', async () => {
  await remote.start(recipe);
  remote.sync({ ...recipe, id: 'other', title: 'Another dish' });
  expect(media.metadata).toMatchObject({ title: 'Butter sauce · Step 1 of 2' });
  cooking.moveTo(recipe.id, 8);
  remote.sync({ ...recipe, steps: recipe.steps.slice(0, 1) });
  expect(media.metadata).toMatchObject({ title: 'Butter sauce · Step 1 of 1' });
  press('nexttrack');
  expect(cooking.session?.currentStepIndex).toBe(8);
  press('previoustrack');
  expect(cooking.session?.currentStepIndex).toBe(8);
});

it('cleans up handlers and audio on exit, leaving timers and cooking intact when only the remote stops', async () => {
  timers.start(0, 120, 'Simmer');
  await remote.start(recipe);
  const oldNext = handlers.get('nexttrack');
  press('stop');
  expect(remote.active).toBe(false);
  expect(media.metadata).toBeNull();
  expect(media.playbackState).toBe('none');
  expect([...handlers.values()].every((handler) => handler === null)).toBe(true);
  expect(audio.removeAttribute).toHaveBeenCalledWith('src');
  expect(audio.load).toHaveBeenCalledOnce();
  expect(cooking.session).not.toBeNull();
  expect(timers.timers).toHaveLength(1);
  await remote.start(recipe);
  oldNext?.({ action: 'nexttrack' });
  expect(cooking.session?.currentStepIndex).toBe(0);
  window.dispatchEvent(new Event('pagehide'));
  expect(remote.active).toBe(false);
});

it('discards a pending activation after exit and recovers from autoplay denial', async () => {
  let arrive!: () => void;
  const starting = remote.start(recipe);
  // The first resolved play is still pending until its microtask runs.
  remote.stop();
  expect(await starting).toBe(false);
  expect(media.metadata).toBeNull();
  vi.stubGlobal(
    'Audio',
    vi.fn(function (src: string) {
      audio = new AudioStub(src);
      audio.play = vi.fn(
        () =>
          new Promise<void>((resolve) => {
            arrive = resolve;
          })
      );
      return audio;
    })
  );
  const pending = remote.start(recipe);
  cooking.reset();
  remote.sync(null);
  arrive();
  expect(await pending).toBe(false);
  expect(remote.active).toBe(false);
  vi.stubGlobal(
    'Audio',
    class extends AudioStub {
      constructor(src: string) {
        super(src);
        this.play = vi.fn().mockRejectedValue(new Error('NotAllowedError'));
      }
    }
  );
  await cooking.resume();
  expect(await remote.start(recipe)).toBe(false);
  expect(remote.starting).toBe(false);
  expect(media.metadata).toBeNull();
});

it('tolerates unsupported actions and missing Media Session APIs', async () => {
  media.setActionHandler.mockImplementation((action, handler) => {
    if (action === 'stop') throw new Error('NotSupportedError');
    handlers.set(action, handler);
  });
  expect(await remote.start(recipe)).toBe(true);
  press('nexttrack');
  expect(cooking.session?.currentStepIndex).toBe(1);
  remote.stop();
  Reflect.deleteProperty(navigator, 'mediaSession');
  expect(remote.supported).toBe(false);
  expect(await remote.start(recipe)).toBe(false);
  render(CookingRemoteToggle, { recipe });
  expect(screen.queryByRole('button')).toBeNull();
});

it('shares UI controls and the shell lifecycle without a reactive request loop', async () => {
  render(KitchenRuntime);
  render(CookingRemoteToggle, { recipe });
  await fireEvent.click(screen.getByRole('button', { name: 'Remote controls' }));
  await waitFor(() =>
    expect(screen.getByRole('button', { name: 'Turn off remote' })).toBeDefined()
  );
  timers.start(0, 120, 'Simmer');
  await tick();
  expect(media.metadata?.title).toContain('Simmer');
  timers.pause(0);
  await tick();
  expect(media.playbackState).toBe('paused');
  timers.resume(0);
  await waitFor(() => expect(media.playbackState).toBe('playing'));
  cooking.moveTo(recipe.id, 1);
  await tick();
  expect(media.metadata?.title).toContain('Step 2 of 2');
  const requests = fetchMock.mock.calls.length;
  await tick();
  await tick();
  expect(fetchMock.mock.calls.length).toBe(requests);
  await cooking.end(false);
  await tick();
  expect(remote.active).toBe(false);
  expect(media.metadata).toBeNull();
});

it('releases remote controls when audio is interrupted while the kitchen timer keeps running', async () => {
  timers.start(0, 120, 'Simmer');
  await remote.start(recipe);
  audio.pause();
  expect(timers.timers[0]?.pausedRemaining).toBeUndefined();
  expect(media.playbackState).toBe('none');
  audio.dispatchEvent(new Event('error'));
  expect(remote.active).toBe(false);
  expect(media.metadata).toBeNull();
});
