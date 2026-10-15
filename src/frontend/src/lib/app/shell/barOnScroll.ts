/**
 * When the bottom bar of a phone gets out of the way: hidden while reading down a page, back on any
 * deliberate scroll up, at the top and at the end. `anchor` is where the bar last could have changed:
 * the highest point while it shows, the lowest while it is hidden, so a turn is measured from there.
 */
export interface BarScroll {
  readonly hidden: boolean;
  readonly anchor: number;
}

/** How far one direction must carry before the bar follows, so a thumb resting on the glass does not flicker it. */
export const travel = 24;

/** Near the top there is nothing to make room for. */
export const nearTop = 64;

export const shown: BarScroll = { hidden: false, anchor: 0 };

/**
 * `byReader`: the reader scrolled (wheel, touch, a scroll key). Only that hides the bar; a scroll the
 * page makes (focus moving to a field, a jump to a found word) leaves it, so nothing slides over what
 * was just brought into view and a keyboard keeps its way out.
 */
export function barAfterScroll(
  before: BarScroll,
  y: number,
  atEnd: boolean,
  byReader = true
): BarScroll {
  if (y <= nearTop || atEnd) {
    return { hidden: false, anchor: y };
  }

  if (before.hidden) {
    return y < before.anchor - travel
      ? { hidden: false, anchor: y }
      : { hidden: true, anchor: Math.max(before.anchor, y) };
  }

  if (!byReader) {
    return { hidden: false, anchor: y };
  }

  return y > before.anchor + travel
    ? { hidden: true, anchor: y }
    : { hidden: false, anchor: Math.min(before.anchor, y) };
}
