import { redirect } from '@sveltejs/kit';

import { resolve } from '$app/paths';

/** The list's old address, still in installed apps' shortcuts and bookmarks; its `?mahlzeit=` comes along. */
export const load = ({ url }) => {
  redirect(308, `${resolve('/(app)/week/shopping')}${url.search}`);
};
