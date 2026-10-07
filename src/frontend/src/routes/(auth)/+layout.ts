import { redirect } from '@sveltejs/kit';

import { resolve } from '$app/paths';

import { readSetup } from '$features/server/setup';

/**
 * Sends everybody to setup until the instance has an administrator, and away afterwards. Here, ahead of sign-in
 * and register, where a fresh instance's first visitor lands; an instance with no database checks setup itself.
 * A server that could not be asked sends nobody anywhere (dropped wifi must not push onto setup).
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
