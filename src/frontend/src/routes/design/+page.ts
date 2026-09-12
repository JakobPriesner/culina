import { error } from '@sveltejs/kit';

/**
 * The gallery exists only while developing.
 *
 * `import.meta.env.DEV` is statically false in a production build, so the page
 * body is removed by the bundler — this makes the route itself behave the same
 * way, rather than serving an empty page at a URL that looks like a feature.
 */
export const load = () => {
  if (!import.meta.env.DEV) {
    error(404, 'Not found');
  }
};
