import { redirect } from '@sveltejs/kit';

import { resolve } from '$app/paths';

import { loginUrlFor } from '$features/auth/redirectTarget';
import { session } from '$features/auth/session.svelte';
import { readSetup } from '$features/server/setup';

/**
 * The guard for everything behind a sign-in.
 *
 * Resolving here rather than in the layout component means the shell is never
 * rendered for a stranger — not even for the frame before a redirect, which is
 * how a signed-out person catches a glimpse of someone else's recipes.
 *
 * The intended URL is preserved, because being sent to the start after signing
 * in is the fastest way to make a link in a message feel broken.
 */
export const load = async ({ url }) => {
  await session.resolve();

  // "We could not ask" is not "you are not signed in". A timeout, a dropped
  // connection or a backend that is still starting up must never cost somebody
  // a session that is perfectly valid — the layout offers to try again, and the
  // cookie is still in the jar when they do.
  if (session.status === 'unavailable') {
    // Unless the reason is that there is nothing to sign in to yet. A server
    // with no database answers every request but setup's with 503, which is
    // "unavailable" from here — and the setup screen, not a retry button, is
    // what that visitor needs.
    const setup = await readSetup();

    if (setup && setup.stage !== 'complete') {
      redirect(307, resolve('/(auth)/setup'));
    }

    return;
  }

  if (session.status !== 'authenticated') {
    redirect(307, loginUrlFor(url));
  }

  const welcome = '/welcome';
  const onWelcome = url.pathname === welcome;
  const hasHousehold = session.households.length > 0;

  // Nothing in the app works without a household, so an account that has none
  // goes to the screen that offers the two ways to get one — rather than to a
  // recipe list that can only be empty.
  if (!hasHousehold && !onWelcome) {
    redirect(307, welcome);
  }

  // And the other way: that screen says "you are not in a household yet", which
  // would be a lie to someone who is. Following an invitation link is the one
  // reason to be there anyway.
  if (hasHousehold && onWelcome && !url.searchParams.has('code')) {
    redirect(307, '/');
  }
};
