import { redirect } from '@sveltejs/kit';

import { instanceOrigin } from '$api';
import { resolve } from '$app/paths';

import { readSetup } from '$features/server/setup';
import type { Setup } from '$features/server/types';

/** The sign-in screen must not hang on a stalled server; the app's own boot wait is this long too. */
const setupDeadlineMs = 4_000;

/** Setup only moves forward, so a finished instance is asked once per page lifetime (per server, as the app can switch). */
let finished: { readonly origin: string | null; readonly setup: Setup } | null = null;

async function setupOfThisInstance(): Promise<Setup | null> {
  const origin = instanceOrigin();

  if (finished?.origin === origin) {
    return finished.setup;
  }

  const setup = await readSetup(setupDeadlineMs);

  if (setup?.stage === 'complete') {
    finished = { origin, setup };
  }

  return setup;
}

/**
 * Sends everybody to setup until the instance has an administrator, and away afterwards. Here, ahead of sign-in
 * and register, where a fresh instance's first visitor lands; an instance with no database checks setup itself.
 * A server that could not be asked sends nobody anywhere (dropped wifi must not push onto setup).
 */
export const load = async ({ url }) => {
  const setup = await setupOfThisInstance();
  const onSetup = url.pathname === resolve('/(auth)/setup');

  if (setup && setup.stage !== 'complete' && !onSetup) {
    redirect(307, resolve('/(auth)/setup'));
  }

  if (setup?.stage === 'complete' && onSetup) {
    redirect(307, resolve('/(auth)/login'));
  }

  return { setup };
};
