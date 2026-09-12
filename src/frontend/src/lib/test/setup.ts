import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/svelte';
import { afterEach } from 'vitest';

import { resetAll } from './resettable';

/*
 * A browser resolves `new Request('/api/v1/…')` against the document. The test
 * environment borrows Node's Request, which demands an absolute URL and throws
 * on the relative paths the generated client is built from. Resolving against
 * the jsdom document's origin is what a browser does, so this restores the
 * environment rather than changing the client to suit it.
 */
const AbsoluteRequest = globalThis.Request;

globalThis.Request = class extends AbsoluteRequest {
  constructor(input: RequestInfo | URL, init?: RequestInit) {
    super(typeof input === 'string' ? new URL(input, document.baseURI).toString() : input, init);
  }
} as typeof Request;

// Components are unmounted and module-level state is restored between tests, so
// one test's dialog cannot be found by the next one's query and one test's
// signed-in user cannot leak into the next one's assertions.
afterEach(() => {
  cleanup();
  resetAll();
  document.documentElement.removeAttribute('data-theme');
  document.documentElement.removeAttribute('data-mode');
});
