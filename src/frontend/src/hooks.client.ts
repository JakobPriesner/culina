import type { ClientInit, HandleClientError } from '@sveltejs/kit';

import { report, startReporting } from '$shell/telemetry';

/** Reports failures before the app exists (a module fails to load, a store throws during the first page). */
export const init: ClientInit = () => {
  startReporting();
};

/** Whatever a load function or navigation threw; the router catches these, so they never reach the window's error handler. */
export const handleError: HandleClientError = ({ error, status }) => {
  // An address nobody has is somebody's typo, not the app being wrong.
  if (status !== 404) {
    report('render_failed', error);
  }
};
