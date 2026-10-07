import { ErrorCodes } from '$api';

import { m } from './i18n';

interface Failure {
  readonly code: string;
  readonly detail: string;
}

type Message = (() => string) | undefined;

const catalogue = m as unknown as Record<string, Message>;

/**
 * What to show when a request fails, in the reader's language: the code is translated
 * (`problem.<code>`, kept complete by `explain.spec.ts`), the detail only as a last resort.
 */
const clientSaid: Record<string, Message> = {
  [ErrorCodes.offline]: m['error.offline'],
  [ErrorCodes.timeout]: m['error.timeout'],
  [ErrorCodes.unexpected]: m['error.unexpected.body']
};

export const explain = (failure: Failure): string =>
  (clientSaid[failure.code] ?? catalogue[`problem.${failure.code}`])?.() ?? failure.detail;
