/**
 * The public face of the API layer.
 *
 * Features import from here. Nothing outside this directory imports the
 * generated schema, and nothing outside it calls `fetch` — a lint rule enforces
 * both, because the moment a component builds its own request it also has to
 * remember CSRF, conditional requests and the shape of a failure.
 */
export { http, request } from './client';
export { ask, watch, type Stream, type StreamHandlers } from './events';
export { clientError, ErrorCodes, type AppError, type FieldProblem } from './problem';
export { err, ok, type Err, type Ok, type Result } from './result';
export { handleSessionExpiry } from './session';
export { forgetEverything as forgetCachedResponses } from './etagCache';
