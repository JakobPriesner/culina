import type { AppError } from './problem';

/**
 * Every call returns one of these. Nothing in the API layer throws for a
 * failure the server described: a 409 is an outcome, not an exception, and a
 * component that forgets to handle it should fail to compile rather than fail
 * at runtime.
 */
export type Result<TValue> = Ok<TValue> | Err;

export interface Ok<TValue> {
  readonly ok: true;
  readonly value: TValue;
}

export interface Err {
  readonly ok: false;
  readonly error: AppError;
}

export const ok = <TValue>(value: TValue): Ok<TValue> => ({ ok: true, value });

export const err = (error: AppError): Err => ({ ok: false, error });
