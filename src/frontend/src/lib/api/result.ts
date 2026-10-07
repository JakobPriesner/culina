import type { AppError } from './problem';

/**
 * Every call returns one of these: a 409 is an outcome, not an exception, and an unhandled one
 * should fail to compile rather than at runtime.
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
