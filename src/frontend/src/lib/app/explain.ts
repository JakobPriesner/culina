import { ErrorCodes, type AppError } from '$api';

import { m } from './i18n';

/**
 * What to put on screen when a request fails.
 *
 * The API layer is deliberately language-free: it runs before anyone has
 * chosen a language and it has no business importing the message catalogue. So
 * the failures it invents itself — offline, timed out — carry an English
 * sentence as a last resort, and this is where they become the reader's
 * language instead.
 *
 * Anything the server said is used as it is. The server already answers in the
 * language the request asked for, and a second translation here could only
 * disagree with it.
 */
const spoken: Record<string, () => string> = {
  [ErrorCodes.offline]: () => m['error.offline'](),
  [ErrorCodes.timeout]: () => m['error.timeout'](),
  [ErrorCodes.unexpected]: () => m['error.unexpected.body']()
};

export const explain = (error: AppError): string => spoken[error.code]?.() ?? error.detail;
