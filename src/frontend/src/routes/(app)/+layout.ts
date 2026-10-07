import { redirect } from '@sveltejs/kit';

import { resolve } from '$app/paths';

import { loginUrlFor } from '$features/auth/redirectTarget';
import { session } from '$features/auth/session.svelte';
import { readSetup } from '$features/server/setup';

/**
 * The guard for everything behind a sign-in. Resolved here, not in the layout component, so the shell never renders for a stranger, even for one frame.
 * The intended URL is preserved through the redirect.
 */
export const load = async ({ url }) => {
  await session.resolve();

  // "Could not ask" is not "signed out": a timeout or a backend still starting must not cost a valid session; the layout offers a retry.
  if (session.status === 'unavailable') {
    // Unless nothing exists to sign in to yet: a server with no database answers all but setup with 503, and that visitor needs the setup screen.
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

  // An account with no household goes to the screen offering the two ways to get one, not to an always-empty recipe list.
  if (!hasHousehold && !onWelcome) {
    redirect(307, welcome);
  }

  // The reverse: "you are not in a household yet" would be a lie to a member; following an invitation link is the one reason to be there.
  if (hasHousehold && onWelcome && !url.searchParams.has('code')) {
    redirect(307, '/');
  }
};
