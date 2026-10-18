import { afterEach, describe, expect, it } from 'vitest';

import { tips } from './tips.svelte';

/* What has been shown is remembered per person on the device, and Settings can bring it all back. */
afterEach(() => {
  tips.reset();
  localStorage.clear();
});

describe('tips seen', () => {
  it('shows a tip until it is put away, and remembers that for the person', () => {
    tips.own('u1');

    expect(tips.isNew('steps')).toBe(true);

    tips.dismiss('steps');
    tips.reset();
    tips.own('u1');

    expect(tips.isNew('steps')).toBe(false);
  });

  it('keeps one person’s tips apart from another’s on a shared device', () => {
    tips.own('u1');
    tips.dismiss('cook');
    tips.own('u2');

    expect(tips.isNew('cook')).toBe(true);
  });

  it('shows nothing before anybody is signed in', () => {
    expect(tips.isNew('plan')).toBe(false);
  });

  it('brings every tip back when asked', () => {
    tips.own('u1');
    tips.dismiss('plan');
    tips.dismiss('tour');

    tips.showAgain();

    expect(tips.isNew('plan')).toBe(true);
    expect(tips.isNew('tour')).toBe(true);
  });

  it('ends the tour for good, offered or opened', () => {
    tips.own('u1');
    tips.startTour();

    tips.endTour();

    expect(tips.touring).toBe(false);
    expect(tips.isNew('tour')).toBe(false);
  });
});
