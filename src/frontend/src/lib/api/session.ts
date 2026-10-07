import { forgetEverything } from './etagCache';

/**
 * The seam between the API layer and the app: the shell registers what happens on a 401, the client
 * only decides when (no router or store cycle).
 */
type ExpiryHandler = () => void;

let onExpired: ExpiryHandler = () => {};

export function handleSessionExpiry(handler: ExpiryHandler): void {
  onExpired = handler;
}

export function sessionExpired(): void {
  forgetEverything();
  onExpired();
}
