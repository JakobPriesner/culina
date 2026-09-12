import { base } from '$app/paths';

/**
 * Where a recipe's photo lives, at a given width.
 *
 * The three widths are the ones the server actually stores; asking for anything
 * else is refused rather than resized on the fly, because a server that resizes
 * on demand is a server anyone can make do arbitrary work.
 */
export const imageWidths = [400, 800, 1600] as const;

export type ImageWidth = (typeof imageWidths)[number];

export const imageUrl = (recipeId: string, width: ImageWidth): string =>
  `${base}/api/v1/recipes/${recipeId}/image?w=${width}`;

/**
 * The candidates a browser picks from, so a phone does not fetch a photo sized
 * for a desktop.
 */
export const imageSrcset = (recipeId: string): string =>
  imageWidths.map((width) => `${imageUrl(recipeId, width)} ${width}w`).join(', ');
