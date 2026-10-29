import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { PlanWindow } from './planWindow.svelte';

// A Wednesday: this week runs from Monday 19 to Sunday 25 October.
beforeEach(() => vi.useFakeTimers({ toFake: ['Date'], now: new Date('2026-10-21T10:00:00') }));
afterEach(() => vi.useRealTimers());

describe('the days the plan shows', () => {
  // First, while nothing has been chosen on this device: the screen's width decides.
  it('opens on the coming days on a phone, and on this week beside the list', () => {
    const phone = new PlanWindow({ week: null, listBeside: false });
    const beside = new PlanWindow({ week: null, listBeside: true });

    expect([phone.view, phone.start, phone.end]).toEqual(['days', '2026-10-21', '2026-10-24']);
    expect([beside.view, beside.start, beside.end]).toEqual(['week', '2026-10-19', '2026-10-25']);
  });

  it('opens on the week asked for', () => {
    const next = new PlanWindow({ week: 1, listBeside: false });

    expect([next.view, next.start, next.offset]).toEqual(['week', '2026-10-26', 1]);
  });

  it('steps a week at a time in the week, and comes back to today', () => {
    const shown = new PlanWindow({ week: 0, listBeside: false });

    shown.step(1);
    expect([shown.start, shown.atToday]).toEqual(['2026-10-26', false]);

    shown.toToday();
    expect([shown.start, shown.atToday]).toEqual(['2026-10-19', true]);
  });

  it('keeps the week in view when the way of looking changes', () => {
    const shown = new PlanWindow({ week: 1, listBeside: false });

    shown.choose('days');
    expect([shown.view, shown.start]).toEqual(['days', '2026-10-26']);

    shown.step(-1);
    shown.step(-1);
    shown.choose('week');
    expect([shown.view, shown.start]).toEqual(['week', '2026-10-19']);

    // This week, looked at as days, starts today rather than with days already over.
    shown.choose('days');
    expect(shown.start).toBe('2026-10-21');
  });

  it('shows and marks a day sought', () => {
    const shown = new PlanWindow({ week: 0, listBeside: false });

    shown.seek('2026-11-04');

    expect([shown.start, shown.sought]).toEqual(['2026-11-02', '2026-11-04']);
  });
});
