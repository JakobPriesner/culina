import { goto } from '$app/navigation';
import { resolve } from '$app/paths';

import { session } from './session.svelte';

/** Ends the session and lands on sign-in, replacing the page so Back cannot return to it; from settings and the account menu alike. */
export async function signOut(): Promise<void> {
  await session.signOut();
  await goto(resolve('/(auth)/login'), { replaceState: true });
}
