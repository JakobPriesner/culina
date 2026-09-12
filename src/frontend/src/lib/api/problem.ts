import type { components } from './generated/schema';

type ProblemDocument = components['schemas']['ProblemDetails'];

/** One wrong field, so a form can mark all of them in a single pass. */
export interface FieldProblem {
  readonly field: string | null;
  readonly code: string;
  readonly detail: string;
}

/**
 * A failure, in the shape the UI needs it.
 *
 * `code` is the only part worth branching on. `detail` is prose written for a
 * person and will be reworded, so a comparison against it is a bug waiting for
 * the next copy edit.
 */
export interface AppError {
  readonly code: string;
  readonly detail: string;
  readonly status: number;
  readonly requestId: string | null;
  readonly fields: readonly FieldProblem[];
}

/** The codes the client itself acts on. Everything else is the UI's business. */
export const ErrorCodes = {
  notAuthenticated: 'auth.not_authenticated',
  csrfInvalid: 'auth.csrf_invalid',
  versionMismatch: 'request.version_mismatch',
  /** The request never reached a server. */
  offline: 'client.offline',
  /** The request was still running when the caller gave up on it. */
  timeout: 'client.timeout',
  /** A response we could not make sense of. */
  unexpected: 'client.unexpected'
} as const;

const isProblem = (body: unknown): body is ProblemDocument =>
  typeof body === 'object' && body !== null && 'code' in body;

/** Reads an RFC 9457 document, falling back to something honest if it is not one. */
export function toAppError(status: number, body: unknown): AppError {
  if (!isProblem(body)) {
    return {
      code: ErrorCodes.unexpected,
      detail: 'Something went wrong. Please try again.',
      status,
      requestId: null,
      fields: []
    };
  }

  return {
    code: body.code,
    detail: body.detail ?? body.title ?? 'Something went wrong. Please try again.',
    status,
    requestId: body.requestId ?? null,
    fields: (body.errors ?? []).map((cause) => ({
      field: cause.field ?? null,
      code: cause.code,
      detail: cause.detail
    }))
  };
}

/** A failure that never reached a server, described the same way as one that did. */
export function clientError(code: string, detail: string): AppError {
  return { code, detail, status: 0, requestId: null, fields: [] };
}

export const offline = () =>
  clientError(ErrorCodes.offline, 'You appear to be offline. The change has not been saved.');

export const timedOut = () =>
  clientError(ErrorCodes.timeout, 'That took too long. Please try again.');
