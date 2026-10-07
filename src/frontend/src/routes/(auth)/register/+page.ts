import { readPolicy } from '$features/auth/registration.svelte';

/** What this instance allows, fetched before the form is drawn so it never grows a field mid-typing. */
export const load = async () => ({ policy: await readPolicy() });
