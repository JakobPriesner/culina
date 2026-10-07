import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/svelte';
import { afterEach } from 'vitest';

/* jsdom needs Request URLs resolved against the document origin like a browser; Node's Request throws on the generated client's relative paths. */
const AbsoluteRequest = globalThis.Request;

globalThis.Request = class extends AbsoluteRequest {
  constructor(input: RequestInfo | URL, init?: RequestInit) {
    super(typeof input === 'string' ? new URL(input, document.baseURI).toString() : input, init);
  }
} as typeof Request;

/* jsdom has no matchMedia; the stub answers no to every query (widest layout), so tests of other layouts set the answer themselves. */
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

// jsdom has no layout or resize notifications. Browser tests exercise the
// measured layouts; component tests only need the observer's lifecycle.
if (typeof globalThis.ResizeObserver !== 'function') {
  globalThis.ResizeObserver = class {
    observe() {}
    unobserve() {}
    disconnect() {}
  };
}

/* jsdom has no Web Animations API (Svelte transitions need it); the stub finishes every animation at once. */
if (typeof Element.prototype.animate !== 'function') {
  Element.prototype.animate = function animate() {
    const animation = {
      playState: 'finished',
      onfinish: null as (() => void) | null,
      finished: Promise.resolve(),
      cancel: () => {},
      finish: () => {}
    };

    queueMicrotask(() => animation.onfinish?.());

    return animation as unknown as Animation;
  };
  Element.prototype.getAnimations = () => [];
}

// Components unmount between tests; stores expose their own `reset`, called by the suites that need it.
afterEach(() => {
  cleanup();
  document.documentElement.removeAttribute('data-theme');
  document.documentElement.removeAttribute('data-mode');
});
