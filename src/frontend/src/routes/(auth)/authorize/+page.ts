import { redirect } from '@sveltejs/kit';

import { clientError, type AppError } from '$api';
import { readAgentAuthorization, type AgentAuthorization } from '$features/auth/agentAuthorization';
import { loginUrlFor } from '$features/auth/redirectTarget';
import { session } from '$features/auth/session.svelte';

type Consent =
  | { readonly authorization: AgentAuthorization; readonly problem: null }
  | { readonly authorization: null; readonly problem: AppError };

const notFound = () =>
  clientError(
    'agent_authorizations.not_found',
    'This connection request has expired or was already answered.'
  );

/**
 * Where Culina's OAuth authorization endpoint sends the browser: `?request=` for a request to answer, `?problem=` when the
 * app's request could not be trusted with a redirect. Answering needs the person signed in, so a stranger signs in
 * first and comes back here.
 */
export const load = async ({ url }): Promise<Consent> => {
  const problem = url.searchParams.get('problem');

  if (problem) {
    return { authorization: null, problem: clientError(problem, problem) };
  }

  await session.resolve();

  // "Could not ask" is not "signed out": the read below then fails and says so, as the app's own guard does.
  if (session.status !== 'authenticated' && session.status !== 'unavailable') {
    redirect(307, loginUrlFor(url));
  }

  const id = url.searchParams.get('request');

  if (!id) {
    return { authorization: null, problem: notFound() };
  }

  const read = await readAgentAuthorization(id);

  return read.ok
    ? { authorization: read.value, problem: null }
    : { authorization: null, problem: read.error };
};
