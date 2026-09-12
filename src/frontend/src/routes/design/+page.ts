import { error } from '@sveltejs/kit';

import { galleryEnabled } from '$shell/gallery';

/**
 * The gallery is not part of the product.
 *
 * `galleryEnabled` is statically false in a release build, so the page body is
 * removed by the bundler; this makes the route itself behave the same way,
 * rather than serving an empty page at a URL that looks like a feature.
 */
export const load = () => {
  if (!galleryEnabled) {
    error(404, 'Not found');
  }
};
