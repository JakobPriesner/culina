import { afterEach, describe, expect, it, vi } from 'vitest';

import { planDayHref, weekOffsetOf } from './weekDates';

afterEach(() => vi.useRealTimers());

describe('the week a date is in', () => {
  it('counts weeks from this one, either way, across a clock change', () => {
    // A Wednesday; the clocks go back in Europe on the Sunday after.
    vi.useFakeTimers({ now: new Date('2026-10-21T10:00:00') });

    expect(weekOffsetOf('2026-10-19')).toBe(0);
    expect(weekOffsetOf('2026-10-25')).toBe(0);
    expect(weekOffsetOf('2026-10-26')).toBe(1);
    expect(weekOffsetOf('2026-11-08')).toBe(2);
    expect(weekOffsetOf('2026-10-18')).toBe(-1);
  });

  it('leads to the plan at that day', () => {
    vi.useFakeTimers({ now: new Date('2026-10-21T10:00:00') });

    expect(planDayHref('2026-10-27')).toBe('/plan?week=1&tag=2026-10-27');
  });
});
