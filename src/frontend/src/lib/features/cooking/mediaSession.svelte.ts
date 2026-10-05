import silentAudio from './cooking-silence.wav?url';
import { SvelteURL } from 'svelte/reactivity';
import type { RecipeReading } from '$features/recipes/types';
import { imageUrl } from '$features/recipes/recipeImage';
import { createScaling } from '$features/recipes/surface/scaled.svelte';
import { parseStep, type Inline } from '$features/recipes/surface/stepMarkdown';
import { registerStore } from '$shell/stores';
import { m } from '$shell/i18n';
import { haptics } from '$shell/haptics';
import { cooking } from './stores/cooking.svelte';
import { kitchenTimers } from './kitchen.svelte';

const actions = ['nexttrack', 'previoustrack', 'play', 'pause', 'stop'] as const;

/** One remote for the same kitchen session, including when its route is hidden. */
export function createCookingMediaSession() {
  let active = $state(false);
  let starting = $state(false);
  let recipe = $state.raw<RecipeReading | null>(null);
  let sessionId: string | null = null;
  let audio: HTMLAudioElement | null = null;
  let generation = 0;
  let pausedTimerStep: number | null = null;
  let pausedByTimer = false;
  let playing = false;
  let intentionalPause = false;
  let lastMetadata = '';
  let lastContext = '';
  let lastPublished = 0;
  const scaling = createScaling(
    () => recipe,
    () => cooking.session?.servings ?? 1
  );
  const supported = () =>
    typeof navigator !== 'undefined' &&
    typeof navigator.mediaSession?.setActionHandler === 'function' &&
    typeof MediaMetadata !== 'undefined';

  function valid() {
    return recipe !== null && sessionId !== null && cooking.session?.sessionId === sessionId;
  }

  function selectedTimer() {
    const eligible = kitchenTimers.timers.filter((timer) =>
      timer.pausedRemaining !== undefined ? timer.pausedRemaining > 0 : timer.endsAt > Date.now()
    );
    return (
      eligible.find((timer) => timer.stepIndex === cooking.session?.currentStepIndex) ??
      eligible.find(
        (timer) => timer.stepIndex === pausedTimerStep && timer.pausedRemaining !== undefined
      ) ??
      eligible
        .filter((timer) => timer.pausedRemaining === undefined)
        .sort((a, b) => a.endsAt - b.endsAt)[0] ??
      eligible.sort((a, b) => a.stepIndex - b.stepIndex)[0]
    );
  }

  function inlineText(nodes: readonly Inline[]): string {
    return nodes
      .map((node) => {
        if (node.kind === 'ingredient')
          return [scaling.amountFor(node).text, node.name].filter(Boolean).join(' ');
        if ('children' in node) return inlineText(node.children);
        return node.text;
      })
      .join('');
  }

  function publish() {
    if (!valid() || !active || !recipe || !supported()) return;
    const index = Math.max(0, Math.min(cooking.session!.currentStepIndex, recipe.steps.length - 1));
    const step = recipe.steps[index];
    const excerpt = step
      ? parseStep(step.segments)
          .map((block) =>
            block.kind === 'paragraph'
              ? inlineText(block.children)
              : block.items.map(inlineText).join(' ')
          )
          .join(' ')
          .replace(/\s+/g, ' ')
          .trim()
          .slice(0, 180)
      : '';
    const timer = selectedTimer();
    const remaining = timer
      ? (timer.pausedRemaining ?? Math.max(0, Math.ceil((timer.endsAt - Date.now()) / 1000)))
      : 0;
    const timerText = timer
      ? `${timer.label} · ${
          timer.pausedRemaining !== undefined
            ? m['cooking.timer.paused']({
                minutes: Math.floor(remaining / 60),
                seconds: String(remaining % 60).padStart(2, '0')
              })
            : m['cooking.timer.running']({
                minutes: Math.floor(remaining / 60),
                seconds: String(remaining % 60).padStart(2, '0')
              })
        }`
      : '';
    const progress = m['cooking.stepOf']({ current: index + 1, total: recipe.steps.length });
    const metadata = {
      title: `${recipe.title} · ${progress}${timerText ? ` · ${timerText}` : ''}`,
      artist: [step?.title, excerpt].filter(Boolean).join(' · '),
      album: m['cooking.nowCooking']({ title: recipe.title }),
      artwork: recipe.imageId
        ? [{ src: new SvelteURL(imageUrl(recipe.id, 400, recipe.imageId), document.baseURI).href }]
        : []
    };
    const key = JSON.stringify(metadata);
    // Step, scaling, artwork and pause changes are immediate; countdowns at most every 10s.
    const context = JSON.stringify([
      recipe.id,
      recipe.title,
      recipe.imageId,
      index,
      progress,
      excerpt,
      step?.title,
      timer?.stepIndex,
      timer?.pausedRemaining !== undefined,
      timer?.endsAt
    ]);
    if (key !== lastMetadata && (context !== lastContext || Date.now() - lastPublished >= 10000)) {
      try {
        navigator.mediaSession.metadata = new MediaMetadata(metadata);
      } catch {
        /* Optional OS presentation. */
      }
      lastMetadata = key;
      lastContext = context;
      lastPublished = Date.now();
    }
    try {
      const playback = timer?.pausedRemaining !== undefined || audio?.paused ? 'paused' : 'playing';
      if (navigator.mediaSession.playbackState !== playback)
        navigator.mediaSession.playbackState = playback;
    } catch {
      /* Partial implementations may expose only handlers. */
    }
  }

  function stop() {
    generation++;
    const owned = sessionId !== null;
    sessionId = null;
    pausedTimerStep = null;
    pausedByTimer = false;
    playing = false;
    intentionalPause = false;
    active = false;
    starting = false;
    recipe = null;
    const previous = audio;
    audio = null;
    if (previous) {
      previous.removeEventListener('pause', interrupted);
      previous.removeEventListener('error', stop);
      previous.pause();
      previous.removeAttribute('src');
      previous.load();
    }
    if (typeof window !== 'undefined') window.removeEventListener('pagehide', stop);
    if (owned && supported()) {
      for (const action of actions) {
        try {
          navigator.mediaSession.setActionHandler(action, null);
        } catch {
          /* Unsupported action. */
        }
      }
      try {
        navigator.mediaSession.metadata = null;
        navigator.mediaSession.playbackState = 'none';
      } catch {
        /* Optional. */
      }
    }
    lastMetadata = '';
    lastContext = '';
    lastPublished = 0;
  }

  function move(delta: number) {
    if (!valid() || !active || !recipe) return;
    const index = Math.max(0, Math.min(cooking.session!.currentStepIndex, recipe.steps.length - 1));
    const next = index + delta;
    if (next < 0 || next >= recipe.steps.length) return;
    pausedTimerStep = null;
    haptics.step();
    cooking.moveTo(recipe.id, next);
    publish();
  }

  function pause() {
    if (!valid() || !active) return;
    const timer = selectedTimer();
    if (timer) {
      pausedTimerStep = timer.stepIndex;
      pausedByTimer = true;
      kitchenTimers.pause(timer.stepIndex);
    }
    pauseAudio();
    publish();
  }

  function interrupted() {
    if (intentionalPause) {
      intentionalPause = false;
      return;
    }
    // Losing audio focus (e.g. to music or a call) does not stop the pan's clock.
    stop();
  }

  function pauseAudio() {
    if (!audio || audio.paused) return;
    intentionalPause = true;
    audio.pause();
  }

  async function play() {
    if (!valid() || !active || !audio || playing) return;
    playing = true;
    const version = generation;
    const timer = selectedTimer();
    try {
      await audio.play();
      if (version !== generation || !valid()) return;
      if (timer?.pausedRemaining !== undefined) kitchenTimers.resume(timer.stepIndex);
      pausedByTimer = false;
      publish();
    } catch {
      if (version === generation) stop();
    } finally {
      if (version === generation) playing = false;
    }
  }

  function followTimer() {
    if (!active || !audio || playing) return;
    const timer = selectedTimer();
    if (timer?.pausedRemaining !== undefined) {
      pausedByTimer = true;
      pauseAudio();
    } else if (timer && pausedByTimer && audio.paused) {
      void play();
    }
  }

  return {
    get supported() {
      return supported();
    },
    get active() {
      return active;
    },
    get starting() {
      return starting;
    },
    stop,
    sync(value: RecipeReading | null) {
      if (!sessionId) return;
      if (!valid()) {
        stop();
        return;
      }
      if (value && value.id === recipe?.id) {
        if (!value.steps.length) {
          stop();
          return;
        }
        recipe = value;
      }
      followTimer();
      publish();
    },
    async start(value: RecipeReading): Promise<boolean> {
      if (!supported() || cooking.session?.recipeId !== value.id || !value.steps.length)
        return false;
      if (starting || active) return active;
      recipe = value;
      sessionId = cooking.session.sessionId;
      starting = true;
      const version = ++generation;
      try {
        audio = new Audio(silentAudio);
        audio.loop = true;
        audio.addEventListener('pause', interrupted);
        audio.addEventListener('error', stop);
        const handlers = {
          nexttrack: () => move(1),
          previoustrack: () => move(-1),
          play: () => void play(),
          pause,
          stop
        };
        for (const action of actions) {
          try {
            navigator.mediaSession.setActionHandler(action, () => {
              // An OS callback already queued before cleanup cannot touch a new session.
              if (version === generation && valid()) handlers[action]();
            });
          } catch {
            /* Unsupported action. */
          }
        }
        window.addEventListener('pagehide', stop);
        // Called directly from the click, before any await, to preserve activation.
        await audio.play();
        if (version !== generation) return false;
        if (!valid()) {
          stop();
          return false;
        }
        active = true;
        starting = false;
        followTimer();
        publish();
        return true;
      } catch {
        if (version === generation) stop();
        return false;
      }
    }
  };
}

export const kitchenMediaSession = createCookingMediaSession();
registerStore(() => kitchenMediaSession.stop());
