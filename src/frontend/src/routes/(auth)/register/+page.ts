import { readPolicy } from '$features/auth/registration.svelte';

/**
 * What this instance allows, fetched before the form is drawn.
 *
 * Asked here rather than in the component so the form appears already knowing
 * which fields it needs — a sign-up form that grows an invitation field a
 * moment after you start typing is a form that moved under your hands.
 */
export const load = async () => ({ policy: await readPolicy() });
