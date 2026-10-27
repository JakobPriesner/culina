/** A flick faster than this lets the sheet go however short the drag, in px/ms (500 px/s, as Material and Vaul do). */
const FLICK = 0.5;

/** A slow drag lets the sheet go once it is pulled down this share of its height. */
const SHARE = 1 / 3;

/**
 * Whether a sheet let go after being pulled down `offset` px at `velocity` px/ms closes, or settles back. Decided on
 * release only, so a drag can always be taken back before the finger lifts.
 */
export function releasesSheet(offset: number, height: number, velocity: number): boolean {
  if (offset <= 0) return false;

  return velocity >= FLICK || offset >= height * SHARE;
}

/** How far the sheet follows a pull: all the way down, barely up, since it is already as tall as it goes. */
export const followed = (offset: number): number => (offset > 0 ? offset : offset / 8);
