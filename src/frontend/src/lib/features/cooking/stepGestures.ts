/**
 * Step navigation without aiming at a button: arrow keys for a laptop on the counter, swipes for a
 * phone on a stand.
 */

/**
 * Anything that already has its own use for an arrow key or a sideways drag. Not buttons or links:
 * they have none, and after every move the step itself (a button) holds the focus.
 */
const OWN_INPUT = [
  'input',
  'textarea',
  'select',
  '[contenteditable="true"]',
  '[role="slider"]',
  '[role="spinbutton"]',
  '[role="tablist"]',
  '[role="radiogroup"]',
  '[role="listbox"]',
  '[role="menu"]',
  '[role="grid"]'
].join(', ');

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
  advance: () => void;
}

export function createStepGestures({ ready, currentStep, move, advance }: StepGestureOptions) {
  let startX = 0;
  let startY = 0;
  let startTime = 0;
  /** Whether the touch now ending began as one of ours; otherwise the start above is an older touch's. */
  let tracking = false;

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
    tracking = Boolean(touch) && ready() && !handlesItself(event.target);
    if (!touch || !tracking) return;

    startX = touch.clientX;
    startY = touch.clientY;
    startTime = Date.now();
  }

  function touchend(event: TouchEvent) {
    const touch = event.changedTouches[0];
    if (!touch || !tracking || !ready()) return;
    tracking = false;

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
