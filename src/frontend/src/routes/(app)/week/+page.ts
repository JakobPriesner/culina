import { redirect } from '@sveltejs/kit';

import { resolve } from '$app/paths';
import { woche } from '$shell/woche.svelte';

/** `/week` alone opens the view used last on this device, as the Woche tab does. */
export const load = () => {
  redirect(
    307,
    woche.view === 'list' ? resolve('/(app)/week/shopping') : resolve('/(app)/week/plan')
  );
};
