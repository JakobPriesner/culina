import { redirect } from '@sveltejs/kit';

import { woche, wocheHref } from '$shell/woche.svelte';

/** `/week` alone opens the view used last on this device, as the Woche tab does. */
export const load = () => {
  redirect(307, wocheHref(woche.view));
};
