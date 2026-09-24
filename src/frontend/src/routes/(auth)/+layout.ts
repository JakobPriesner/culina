import { redirect } from '@sveltejs/kit';

import { resolve } from '$app/paths';

import { readSetup } from '$features/server/setup';

/**
 * Sends everybody to setup until the instance has an administrator, and away
 * from it afterwards.
 *
 * Here, in front of signing in and registering, because those are where a
 * fresh instance sends its first visitor: the app's own guard finds nobody
 * signed in and comes this way. An instance with no database answers that
 * guard with "unavailable" instead, and it checks setup itself.
 *
 * A server that could not be asked sends nobody anywhere. Being pushed onto a
 * setup screen because the wifi dropped would be worse than a sign-in form
 * that fails honestly.
 */
export const load = async ({ url }) => {
  const setup = await readSetup();
  const onSetup = url.pathname === resolve('/(auth)/setup');

  if (setup && setup.stage !== 'complete' && !onSetup) {
    redirect(307, resolve('/(auth)/setup'));
  }

  if (setup?.stage === 'complete' && onSetup) {
    redirect(307, resolve('/(auth)/login'));
  }

  return { setup };
};
