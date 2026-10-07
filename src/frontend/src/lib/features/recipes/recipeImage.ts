import { base } from '$app/paths';

/** Photo sizes the server stores; other widths are refused, not resized on demand (arbitrary work for anyone). */
export const imageWidths = [400, 800, 1600] as const;

export type ImageWidth = (typeof imageWidths)[number];

/**
 * A recipe photo URL at a width. `version` is the current picture's id: the address is fixed, so without it a replaced
 * picture is not refetched; omitted, it updates on the next load.
 */
export const imageUrl = (recipeId: string, width: ImageWidth, version?: string | null): string =>
  `${base}/api/v1/recipes/${recipeId}/image?w=${width}${version ? `&v=${version}` : ''}`;

/** The srcset candidates, so a phone does not fetch a desktop-sized photo. */
export const imageSrcset = (recipeId: string, version?: string | null): string =>
  imageWidths.map((width) => `${imageUrl(recipeId, width, version)} ${width}w`).join(', ');

/** Where your photo of one attempt lives: personal, so served under the entry that owns it. */
export const attemptUrl = (recipeId: string, entryId: string, width: ImageWidth): string =>
  `${base}/api/v1/recipes/${recipeId}/cook-log/${entryId}/photo?w=${width}`;

export const attemptSrcset = (recipeId: string, entryId: string): string =>
  imageWidths.map((width) => `${attemptUrl(recipeId, entryId, width)} ${width}w`).join(', ');

/** The same photo for a link visitor, who holds a token and no recipe id; the id-addressed image keeps its membership check. */
export const sharedImageUrl = (token: string, width: ImageWidth): string =>
  `${base}/api/v1/shared-recipes/${encodeURIComponent(token)}/image?w=${width}`;

export const sharedImageSrcset = (token: string): string =>
  imageWidths.map((width) => `${sharedImageUrl(token, width)} ${width}w`).join(', ');
