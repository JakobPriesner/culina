import { error } from '@sveltejs/kit';

import { galleryEnabled } from '$shell/gallery';

/**
 * The gallery is not part of the product: this makes the route itself vanish in release builds, as
 * the page body does.
 */
export const load = () => {
  if (!galleryEnabled) {
    error(404, 'Not found');
  }
};
