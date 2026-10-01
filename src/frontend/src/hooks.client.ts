import type { ClientInit, HandleClientError } from '@sveltejs/kit';

import { report, startReporting } from '$shell/telemetry';

/**
 * Before the app is: a module that fails to load or a store that throws while
 * the first page is still being put together is exactly what nobody would
 * otherwise hear about.
 */
export const init: ClientInit = () => {
  startReporting();
};

/**
 * Whatever a load function or a navigation threw. The router catches these, so
 * they never reach the window's own error handler.
 */
export const handleError: HandleClientError = ({ error, status }) => {
  // An address nobody has is somebody's typo, not the app being wrong.
  if (status !== 404) {
    report('render_failed', error);
  }
};
