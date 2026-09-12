import { forgetEverything } from './etagCache';

/**
 * The seam between the API layer and the app.
 *
 * A 401 has to clear every store and route to the sign-in page, but the API
 * layer must not know that a router or a store exists — that would be a cycle,
 * and it would make the client untestable without the whole app. The shell
 * registers what should happen; the client only decides when.
 */
type ExpiryHandler = () => void;

let onExpired: ExpiryHandler = () => {};

/** Registered once by the app shell. */
export function handleSessionExpiry(handler: ExpiryHandler): void {
  onExpired = handler;
}

/** Called when the server says the session is gone. Never retried. */
export function sessionExpired(): void {
  forgetEverything();
  onExpired();
}
