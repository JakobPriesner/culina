/** The API layer's public face: only this directory touches the generated schema or `fetch` (lint-enforced), so no component repeats CSRF, conditional requests or failure mapping. */
export { http, request } from './client';
export { ask, watch, type Stream, type StreamHandlers } from './events';
export { clientError, ErrorCodes, type AppError, type FieldProblem } from './problem';
export { err, ok, type Err, type Ok, type Result } from './result';
export { handleSessionExpiry } from './session';
export { forgetEverything as forgetCachedResponses } from './etagCache';
