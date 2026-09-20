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

/**
 * Where a recipe's photo lives, at a given width.
 *
 * The version is the id of the picture currently on the recipe, and it is what
 * makes replacing one visible. A recipe's image has a fixed address, so a new
 * picture arrives at the address the old one is already displayed from — and a
 * browser asked to show an `src` it is already showing does not go and look
 * again. Drawing a picture for a recipe that had one therefore appeared to do
 * nothing at all.
 *
 * Omitted where the caller does not know it, which is honest rather than
 * harmless: those pictures update on the next load instead of at once.
 */
export const imageUrl = (recipeId: string, width: ImageWidth, version?: string | null): string =>
  `${base}/api/v1/recipes/${recipeId}/image?w=${width}${version ? `&v=${version}` : ''}`;

/**
 * The candidates a browser picks from, so a phone does not fetch a photo sized
 * for a desktop.
 */
export const imageSrcset = (recipeId: string, version?: string | null): string =>
  imageWidths.map((width) => `${imageUrl(recipeId, width, version)} ${width}w`).join(', ');

/**
 * Where your photograph of one attempt lives.
 *
 * A different address from the recipe's own picture because it is a different
 * thing: the recipe's is what the dish is supposed to look like, and this is
 * what it looked like on a Tuesday. It is also personal, so it is served under
 * the entry that owns it.
 */
export const attemptUrl = (recipeId: string, entryId: string, width: ImageWidth): string =>
  `${base}/api/v1/recipes/${recipeId}/cook-log/${entryId}/photo?w=${width}`;

export const attemptSrcset = (recipeId: string, entryId: string): string =>
  imageWidths.map((width) => `${attemptUrl(recipeId, entryId, width)} ${width}w`).join(', ');

/**
 * The same photograph, for whoever followed a link to it.
 *
 * A second address rather than a second parameter on `imageUrl`: a visitor
 * holds a token and no recipe id, and the id-addressed image stays behind the
 * membership check it has always had.
 */
export const sharedImageUrl = (token: string, width: ImageWidth): string =>
  `${base}/api/v1/shared-recipes/${encodeURIComponent(token)}/image?w=${width}`;

export const sharedImageSrcset = (token: string): string =>
  imageWidths.map((width) => `${sharedImageUrl(token, width)} ${width}w`).join(', ');
