import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/svelte';
import { afterEach } from 'vitest';

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

/*
 * jsdom has no `matchMedia` at all — not a stub, not a throwing one, nothing.
 * A component that asks the browser how wide it is therefore fails on import
 * rather than answering "narrow", which is not a distinction any component
 * should have to know about.
 *
 * The stub answers no to every query and never changes, so a layout that adapts
 * renders the arrangement it would use on the widest screen. A test about the
 * other arrangement sets the answer itself.
 */
if (typeof globalThis.matchMedia !== 'function') {
  globalThis.matchMedia = ((query: string) => ({
    matches: false,
    media: query,
    onchange: null,
    addEventListener: () => {},
    removeEventListener: () => {},
    addListener: () => {},
    removeListener: () => {},
    dispatchEvent: () => false
  })) as typeof matchMedia;
}

// Components are unmounted between tests, so one test's dialog cannot be found
// by the next one's query. A store that holds state exposes its own `reset`,
// which its suite calls — explicit, and visible in the test that needs it.
afterEach(() => {
  cleanup();
  document.documentElement.removeAttribute('data-theme');
  document.documentElement.removeAttribute('data-mode');
});
