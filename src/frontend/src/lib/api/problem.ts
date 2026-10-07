import type { components } from './generated/schema';

type ProblemDocument = components['schemas']['ProblemDetails'];

export interface FieldProblem {
  readonly field: string | null;
  readonly code: string;
  readonly detail: string;
}

/**
 * A failure in the shape the UI needs; branch on `code`, since `detail` is prose that will be
 * reworded.
 */
export interface AppError {
  readonly code: string;
  readonly detail: string;
  readonly status: number;
  readonly requestId: string | null;
  readonly fields: readonly FieldProblem[];
  /** Seconds to wait before retrying, from `Retry-After` on a 429. */
  readonly retryAfterSeconds: number | null;
}

export const ErrorCodes = {
  notAuthenticated: 'auth.not_authenticated',
  invalidCredentials: 'auth.invalid_credentials',
  invalidRecoveryCode: 'auth.invalid_recovery_code',
  incorrectPassword: 'users.incorrect_password',
  csrfInvalid: 'auth.csrf_invalid',
  versionMismatch: 'request.version_mismatch',
  offline: 'client.offline',
  timeout: 'client.timeout',
  unexpected: 'client.unexpected'
} as const;

const isProblem = (body: unknown): body is ProblemDocument =>
  typeof body === 'object' && body !== null && 'code' in body;

export function toAppError(response: Response, body: unknown): AppError {
  const retryAfterSeconds = readRetryAfter(response);

  if (!isProblem(body)) {
    return {
      code: ErrorCodes.unexpected,
      detail: 'Something went wrong. Please try again.',
      status: response.status,
      requestId: null,
      fields: [],
      retryAfterSeconds
    };
  }

  return {
    code: body.code,
    detail: body.detail ?? body.title ?? 'Something went wrong. Please try again.',
    status: response.status,
    requestId: body.requestId ?? null,
    fields: (body.errors ?? []).map((cause) => ({
      field: cause.field ?? null,
      code: cause.code,
      detail: cause.detail
    })),
    retryAfterSeconds
  };
}

/**
 * `Retry-After` is seconds or an HTTP date; both become seconds from now, never negative on a wrong
 * client clock.
 */
function readRetryAfter(response: Response): number | null {
  const header = response.headers.get('Retry-After');

  if (!header) {
    return null;
  }

  const seconds = Number(header);

  if (Number.isFinite(seconds)) {
    return Math.max(0, Math.round(seconds));
  }

  const when = Date.parse(header);

  return Number.isNaN(when) ? null : Math.max(0, Math.round((when - Date.now()) / 1000));
}

export function clientError(code: string, detail: string): AppError {
  return { code, detail, status: 0, requestId: null, fields: [], retryAfterSeconds: null };
}

export const offline = () =>
  clientError(ErrorCodes.offline, 'You appear to be offline. The change has not been saved.');

export const timedOut = () =>
  clientError(ErrorCodes.timeout, 'That took too long. Please try again.');
