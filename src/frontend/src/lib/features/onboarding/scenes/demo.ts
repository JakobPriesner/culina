import { untrack } from 'svelte';
import { prefersReducedMotion } from 'svelte/motion';

import { olliSetting } from '$shell/olli/setting.svelte';

/** One moment of a demo: what changes, and when (ms after it starts). */
export type Beat = readonly [at: number, run: () => void];

/** Whether demos play themselves out; off with Olli's motion setting or the system's reduced motion. */
export const moving = (): boolean => olliSetting.animated && !prefersReducedMotion.current;

/**
 * Plays a demo's beats at their times, or all of them at once where nothing should move, so a still
 * screen shows how it ends. Returns how to stop it (a scene left half way).
 */
export function play(beats: readonly Beat[], animated = moving()): () => void {
  if (!animated) {
    // Run from an effect, a beat reading state would make that effect depend on it.
    untrack(() => beats.forEach(([, run]) => run()));

    return () => {};
  }

  const timers = beats.map(([at, run]) => setTimeout(run, at));

  return () => timers.forEach(clearTimeout);
}

/** Beats that type `text` one letter at a time from `start`, each call given the text so far; ends at the returned time. */
export function typing(
  text: string,
  start: number,
  show: (typed: string) => void,
  perLetter = 38
): { beats: Beat[]; end: number } {
  const beats = [...text].map((_, i): Beat => [
    start + i * perLetter,
    () => show(text.slice(0, i + 1))
  ]);

  return { beats, end: start + text.length * perLetter };
}
