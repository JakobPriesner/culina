import { afterEach, describe, expect, it, vi } from 'vitest';

import { whenVisible } from './whenVisible';

/* jsdom has no IntersectionObserver; this stands one in to test when a list asks for its next page. */
interface Watcher {
  readonly element: Element;
  readonly options: IntersectionObserverInit | undefined;
  readonly disconnected: () => boolean;
  enter(): void;
  leave(): void;
}

function stubObserver(): Watcher[] {
  const watchers: Watcher[] = [];

  class Stub {
    #callback: IntersectionObserverCallback;
    #options: IntersectionObserverInit | undefined;

    constructor(callback: IntersectionObserverCallback, options?: IntersectionObserverInit) {
      this.#callback = callback;
      this.#options = options;
    }

    observe(element: Element) {
      let disconnected = false;

      const report = (isIntersecting: boolean) =>
        this.#callback(
          [{ isIntersecting, target: element } as IntersectionObserverEntry],
          this as unknown as IntersectionObserver
        );

      watchers.push({
        element,
        options: this.#options,
        disconnected: () => disconnected,
        enter: () => report(true),
        leave: () => report(false)
      });

      this.disconnect = () => (disconnected = true);
    }

    disconnect() {}
    unobserve() {}
    takeRecords() {
      return [];
    }
  }

  vi.stubGlobal('IntersectionObserver', Stub);

  return watchers;
}

// A stub left behind would tell the next suite it runs in a browser that can watch.
afterEach(() => vi.unstubAllGlobals());

const attach = (reach: () => void, margin?: string) => {
  const element = document.createElement('div');
  const cleanup = whenVisible(reach, margin)(element);

  return { element, cleanup };
};

describe('reaching an element', () => {
  it('runs the work when it comes into view', () => {
    const watchers = stubObserver();
    const reach = vi.fn();

    attach(reach);
    watchers[0]!.enter();

    expect(reach).toHaveBeenCalledTimes(1);
  });

  it('does nothing while it is out of view', () => {
    const watchers = stubObserver();
    const reach = vi.fn();

    attach(reach);
    watchers[0]!.leave();

    expect(reach).not.toHaveBeenCalled();
  });

  // The next page is asked for before the end is on screen, so rows are usually there already.
  it('starts looking before the element is on screen', () => {
    const watchers = stubObserver();

    attach(vi.fn());

    expect(watchers[0]!.options?.rootMargin).toBe('600px');
  });

  it('stops watching when the element goes away', () => {
    const watchers = stubObserver();

    const { cleanup } = attach(vi.fn());

    cleanup?.();

    expect(watchers[0]!.disconnected()).toBe(true);
  });

  it('is harmless where the browser cannot watch at all', () => {
    vi.stubGlobal('IntersectionObserver', undefined);

    expect(() => attach(vi.fn())).not.toThrow();
  });
});
