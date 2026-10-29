import { goto } from '$app/navigation';
import { resolve } from '$app/paths';

import { session } from './session.svelte';

/** Ends the session and lands on sign-in, replacing the page so Back cannot return to it; from settings and the account menu alike. */
export async function signOut(): Promise<void> {
  await session.signOut();
  await goto(resolve('/(auth)/login'), { replace: true });
}

/** The app's "use a different server": signs out of this one, forgets it, and asks for another address. */
export async function switchServer(): Promise<void> {
  // Loaded here, not at the top: the website never switches servers and should not carry the code.
  const { disconnect } = await import('$api/connect');

  await session.signOut();
  await disconnect();
  await goto(resolve('/(auth)/connect'), { replace: true });
}
