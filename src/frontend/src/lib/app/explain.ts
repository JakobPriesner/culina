import { ErrorCodes } from '$api';

import { m } from './i18n';

/** A failure as far as saying it goes: an `AppError`, or one field of one. */
interface Failure {
  readonly code: string;
  readonly detail: string;
}

type Message = (() => string) | undefined;

const catalogue = m as unknown as Record<string, Message>;

/**
 * What to put on screen when a request fails, in the reader's language.
 *
 * The code is what gets translated, never the detail. The API speaks English
 * whatever the request asked for — its details are written for a log — and the
 * client invents a few failures of its own (offline, timed out) before anyone
 * has chosen a language. So every code a server or the client can produce has
 * a `problem.<code>` message, and `explain.spec.ts` reads the backend's error
 * catalogue to keep it that way.
 *
 * The detail is only the last resort, for a code newer than this build.
 */
const clientSaid: Record<string, Message> = {
  [ErrorCodes.offline]: m['error.offline'],
  [ErrorCodes.timeout]: m['error.timeout'],
  [ErrorCodes.unexpected]: m['error.unexpected.body']
};

export const explain = (failure: Failure): string =>
  (clientSaid[failure.code] ?? catalogue[`problem.${failure.code}`])?.() ?? failure.detail;
