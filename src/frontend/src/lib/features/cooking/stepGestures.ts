/**
 * Moving between steps without aiming at a button.
 *
 * A cook's hands are busy and the target should be the phone rather than a
 * button on it: arrow keys for a laptop propped on the counter, horizontal
 * swipes for a phone on a stand.
 */

/** Anything that already has its own use for a key press or a touch. */
const OWN_INPUT = 'input, textarea, select, button, a, [contenteditable="true"]';

/** A deliberate swipe: far enough, mostly sideways, and quick. */
const SWIPE_MIN_DISTANCE = 50;
const SWIPE_HORIZONTAL_BIAS = 1.5;
const SWIPE_MAX_MILLISECONDS = 600;

function handlesItself(target: EventTarget | null): boolean {
  return target instanceof HTMLElement && target.closest(OWN_INPUT) !== null;
}

interface StepGestureOptions {
  ready: () => boolean;
  currentStep: () => number;
  move: (index: number) => void;
  /** Forward, or done. */
  advance: () => void;
}

export function createStepGestures({ ready, currentStep, move, advance }: StepGestureOptions) {
  let startX = 0;
  let startY = 0;
  let startTime = 0;

  function keydown(event: KeyboardEvent) {
    if (!ready() || event.defaultPrevented || handlesItself(event.target)) {
      return;
    }

    if (event.key === 'ArrowRight' || event.key === 'PageDown') {
      event.preventDefault();
      move(currentStep() + 1);
    } else if (event.key === 'ArrowLeft' || event.key === 'PageUp') {
      event.preventDefault();
      move(currentStep() - 1);
    }
  }

  function touchstart(event: TouchEvent) {
    const touch = event.changedTouches[0];
    if (!touch || !ready() || handlesItself(event.target)) return;

    startX = touch.clientX;
    startY = touch.clientY;
    startTime = Date.now();
  }

  function touchend(event: TouchEvent) {
    const touch = event.changedTouches[0];
    if (!touch || !ready()) return;

    const deltaX = touch.clientX - startX;
    const deltaY = touch.clientY - startY;
    const elapsed = Date.now() - startTime;

    // Slow vertical scrolling must not be mistaken for a step move.
    const deliberate =
      Math.abs(deltaX) > SWIPE_MIN_DISTANCE &&
      Math.abs(deltaX) > Math.abs(deltaY) * SWIPE_HORIZONTAL_BIAS &&
      elapsed < SWIPE_MAX_MILLISECONDS;

    if (!deliberate) return;

    if (deltaX < 0) {
      advance();
    } else {
      move(currentStep() - 1);
    }
  }

  return { keydown, touchstart, touchend };
}
