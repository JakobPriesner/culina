import { redirect } from '@sveltejs/kit';

import { resolve } from '$app/paths';
import { instanceOrigin, loadInstance, native } from '$api';

// Culina is a static SPA: there is no Node server in production, so nothing is
// rendered or prerendered on the server. Everything the client can see is
// public by definition, and no secret can leak into the bundle.
export const ssr = false;
export const prerender = false;
export const trailingSlash = 'never';

/**
 * The installed app's first question, before any route asks the API: which server. The website is served by its server
 * and never gets past the first line (`native` is a build-time literal).
 */
export const load = async ({ url }) => {
  if (!native) {
    return;
  }

  const { startNativeShell } = await import('$shell/nativeShell');

  await startNativeShell();
  await loadInstance();

  const connect = resolve('/(auth)/connect');

  if (!instanceOrigin() && url.pathname !== connect) {
    redirect(307, connect);
  }
};
