/** The API layer's public face: only this directory touches the generated schema or `fetch` (lint-enforced), so no component repeats CSRF, conditional requests or failure mapping. */
export { http, request } from './client';
export { ask, watch, type Stream, type StreamHandlers } from './events';
export { clientError, ErrorCodes, type AppError, type FieldProblem } from './problem';
export { err, ok, type Err, type Ok, type Result } from './result';
export { handleSessionExpiry } from './session';
export { forgetEverything as forgetCachedResponses } from './etagCache';
export { native, nativePlatform, relayUrl, type NativePlatform } from './platform';
export { registerAtRelay, revokeAtRelay } from './relay';
export {
  apiUrl,
  instanceOrigin,
  loadInstance,
  publicOrigin,
  rememberSessionToken
} from './instance';
export {
  connect,
  disconnect,
  normaliseAddress,
  supportedApiVersions,
  type ConnectFailure
} from './connect';
export { useSecretStore, type SecretStore } from './secrets';
export { fetchFile } from './files';
export { forgetOfflineReads } from './transport';
