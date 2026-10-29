import type { ClientInit, HandleClientError } from '@sveltejs/kit/hooks';

import { report, startReporting } from '$shell/telemetry';

/** Reports failures before the app exists (a module fails to load, a store throws during the first page). */
export const init: ClientInit = () => {
  startReporting();
};

/** Whatever a load function or navigation threw; the router catches these, so they never reach the window's error handler. */
export const handleError: HandleClientError = (caught) => {
  // `error(...)` is the app saying no on purpose, and an address nobody has is somebody's typo: neither is the app being wrong.
  if (caught.kind === 'app' || (caught.kind === 'framework' && caught.error.status === 404)) {
    return;
  }

  report('render_failed', caught.error);
};
