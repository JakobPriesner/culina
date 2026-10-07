import { describe, expect, it } from 'vitest';

import { imageSrcset, imageUrl } from './recipeImage';

/** Replacing a recipe's picture must change its fetch address, since a browser does not re-fetch a `src` it is already showing. */
describe('a recipe image address', () => {
  it('changes when the picture does', () => {
    const before = imageUrl('recipe-1', 800, 'image-1');
    const after = imageUrl('recipe-1', 800, 'image-2');

    expect(before).not.toBe(after);
  });

  it('stays the same for the same picture, so it is still cacheable', () => {
    expect(imageUrl('recipe-1', 800, 'image-1')).toBe(imageUrl('recipe-1', 800, 'image-1'));
  });

  it('carries the version into every candidate width', () => {
    const candidates = imageSrcset('recipe-1', 'image-1').split(', ');

    expect(candidates).toHaveLength(3);
    expect(candidates.every((candidate) => candidate.includes('v=image-1'))).toBe(true);
  });

  it('asks for a plain address where the caller does not know the picture', () => {
    // A planned meal or cookbook cover holds only a recipe id: it gets the server's picture, and a replaced one on the next load.
    expect(imageUrl('recipe-1', 400)).not.toContain('v=');
    expect(imageUrl('recipe-1', 400, null)).not.toContain('v=');
  });
});
