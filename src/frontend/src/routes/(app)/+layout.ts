import { redirect } from '@sveltejs/kit';

import { loginUrlFor } from '$features/auth/redirectTarget';
import { session } from '$features/auth/session.svelte';

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

  if (session.status !== 'authenticated') {
    redirect(307, loginUrlFor(url));
  }
};
