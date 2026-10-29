import { redirect } from '@sveltejs/kit';

import { resolve } from '$app/paths';

/** The plan's old address, still in installed apps' shortcuts and bookmarks; its `?week=` and `?tag=` come along. */
export const load = ({ url }) => {
  redirect(308, `${resolve('/(app)/week/plan')}${url.search}`);
};
