/**
 * When the bottom bar of a phone recedes, as an iOS tab bar minimises: compact while reading down a page, full again on
 * any deliberate scroll up, at the top and at the end. `anchor` is where the bar last could have changed: the highest
 * point while it is full, the lowest while it is compact, so a turn is measured from there.
 */
export interface BarScroll {
  readonly compact: boolean;
  readonly anchor: number;
}

/** How far one direction must carry before the bar follows, so a thumb resting on the glass does not flicker it. */
export const travel = 24;

/** Near the top there is nothing to make room for. */
export const nearTop = 64;

export const shown: BarScroll = { compact: false, anchor: 0 };

/**
 * `byReader`: the reader scrolled (wheel, touch, a scroll key). Only that makes the bar compact; a scroll the
 * page makes (focus moving to a field, a jump to a found word) leaves it, so nothing changes under what was
 * just brought into view.
 */
export function barAfterScroll(
  before: BarScroll,
  y: number,
  atEnd: boolean,
  byReader = true
): BarScroll {
  if (y <= nearTop || atEnd) {
    return { compact: false, anchor: y };
  }

  if (before.compact) {
    return y < before.anchor - travel
      ? { compact: false, anchor: y }
      : { compact: true, anchor: Math.max(before.anchor, y) };
  }

  if (!byReader) {
    return { compact: false, anchor: y };
  }

  return y > before.anchor + travel
    ? { compact: true, anchor: y }
    : { compact: false, anchor: Math.min(before.anchor, y) };
}
