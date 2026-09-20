import { describe, expect, it } from 'vitest';

import { imageSrcset, imageUrl } from './recipeImage';

/**
 * That replacing a recipe's picture changes where it is fetched from.
 *
 * A recipe's image has one address, so a new picture — uploaded or drawn —
 * arrives at the address the old one is already on screen from. A browser does
 * not go and look again at a `src` it is already showing, so without something
 * in the address that moved, drawing a picture for a recipe that had one
 * appeared to do nothing at all.
 */
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
    // A planned meal and a cookbook cover hold a recipe id and nothing else.
    // They get the picture the server has, and a replaced one on the next load
    // rather than at once.
    expect(imageUrl('recipe-1', 400)).not.toContain('v=');
    expect(imageUrl('recipe-1', 400, null)).not.toContain('v=');
  });
});
