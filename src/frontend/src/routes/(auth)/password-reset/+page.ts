import { readPolicy } from '$features/auth/registration.svelte';

/** Whether this instance sends mail, which decides the first step; fetched before the page is drawn so it never swaps forms mid-typing. */
export const load = async () => ({ policy: await readPolicy() });
