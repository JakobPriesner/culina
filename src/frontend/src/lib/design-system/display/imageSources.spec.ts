import { describe, expect, it } from 'vitest';

import { pickCandidate } from './imageSources';

/* What `srcset` would have chosen, for the app, which has to fetch a photo before showing it. */
const srcset = '/p?w=400 400w, /p?w=800 800w, /p?w=1600 1600w';

describe('choosing a photo width by hand', () => {
  it('takes the smallest candidate that fills the box on this screen', () => {
    expect(pickCandidate('/p?w=1600', srcset, 300, 2)).toBe('/p?w=800');
    expect(pickCandidate('/p?w=1600', srcset, 380, 1)).toBe('/p?w=400');
  });

  it('takes the widest when none is wide enough', () => {
    expect(pickCandidate('/p?w=400', srcset, 1200, 2)).toBe('/p?w=1600');
  });

  it('falls back to the plain source without candidates or a measured box', () => {
    expect(pickCandidate('/p?w=800', undefined, 300, 2)).toBe('/p?w=800');
    expect(pickCandidate('/p?w=800', srcset, 0, 2)).toBe('/p?w=800');
  });
});
