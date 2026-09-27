import { describe, expect, it, vi } from 'vitest';

import { haptics } from './haptics';

describe('haptics', () => {
  it('calls navigator.vibrate with appropriate durations when supported', () => {
    const vibrate = vi.fn();
    vi.stubGlobal('navigator', { vibrate });

    haptics.tick();
    expect(vibrate).toHaveBeenCalledWith(10);

    haptics.step();
    expect(vibrate).toHaveBeenCalledWith(25);

    haptics.celebrate();
    expect(vibrate).toHaveBeenCalledWith([15, 60, 30]);

    haptics.alarm();
    expect(vibrate).toHaveBeenCalledWith([250, 100, 250, 100, 500]);

    vi.unstubAllGlobals();
  });

  it('degrades silently when navigator.vibrate is not available', () => {
    vi.stubGlobal('navigator', {});

    expect(() => {
      haptics.tick();
      haptics.step();
      haptics.celebrate();
      haptics.alarm();
    }).not.toThrow();

    vi.unstubAllGlobals();
  });
});
