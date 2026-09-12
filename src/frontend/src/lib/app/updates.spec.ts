import { beforeEach, describe, expect, it, vi } from 'vitest';

import { toaster } from './toaster.svelte';
import { watchForUpdates } from './updates.svelte';

/*
 * A new version must never take over on its own.
 *
 * Replacing the running build's assets underneath a page means the next chunk
 * it loads comes from a build that no longer matches what is on screen — in a
 * kitchen, that is the app breaking at step four with flour on someone's hands.
 * These tests pin the conversation: notice, offer, and only then reload.
 */
class FakeWorker implements Pick<ServiceWorker, 'postMessage'> {
  messages: unknown[] = [];

  postMessage(message: unknown): void {
    this.messages.push(message);
  }
}

function fakeServiceWorker(options: { waiting?: FakeWorker; controlled: boolean }) {
  const listeners = new Map<string, Set<(event: Event) => void>>();

  const registration = {
    waiting: options.waiting ?? null,
    installing: null,
    update: vi.fn(),
    addEventListener: vi.fn()
  };

  return {
    container: {
      controller: options.controlled ? {} : null,
      register: vi.fn(() => Promise.resolve(registration)),
      addEventListener: (type: string, listener: (event: Event) => void) => {
        const existing = listeners.get(type) ?? new Set();

        existing.add(listener);
        listeners.set(type, existing);
      }
    },
    registration,
    /** Fires the event the browser fires once a new worker has taken over. */
    takeControl: () => {
      for (const listener of listeners.get('controllerchange') ?? []) {
        listener(new Event('controllerchange'));
      }
    }
  };
}

/** Lets the registration promise settle before anything is asserted. */
const settle = () => new Promise((resolve) => setTimeout(resolve, 0));

describe('watching for a new version', () => {
  beforeEach(() => {
    toaster.reset();
  });

  it('does nothing at all where service workers do not exist', () => {
    const navigatorWithout = {} as Navigator;

    vi.stubGlobal('navigator', navigatorWithout);

    // No throw, and a stop function that is safe to call.
    watchForUpdates()();

    expect(toaster.toasts).toHaveLength(0);

    vi.unstubAllGlobals();
  });

  it('offers a version that was installed while the app was closed', async () => {
    const waiting = new FakeWorker();
    const fake = fakeServiceWorker({ waiting, controlled: true });

    vi.stubGlobal('navigator', { serviceWorker: fake.container });

    const stop = watchForUpdates();

    await settle();

    const [toast] = toaster.toasts;

    expect(fake.container.register).toHaveBeenCalledWith('/service-worker.js', { type: 'module' });
    expect(toast).toBeDefined();
    // It never expires: a version that is ready and then never mentioned again
    // is a version nobody installs.
    expect(toast!.durationMs).toBe(0);
    // And it is an offer, not a countdown.
    expect(waiting.messages).toEqual([]);

    stop();
    vi.unstubAllGlobals();
  });

  it('says nothing about the very first worker, which interrupts nobody', async () => {
    const fake = fakeServiceWorker({ waiting: new FakeWorker(), controlled: false });

    vi.stubGlobal('navigator', { serviceWorker: fake.container });

    const stop = watchForUpdates();

    await settle();

    expect(toaster.toasts).toHaveLength(0);

    stop();
    vi.unstubAllGlobals();
  });

  it('hands over only when the offer is accepted, and reloads after it', async () => {
    const waiting = new FakeWorker();
    const fake = fakeServiceWorker({ waiting, controlled: true });
    const reload = vi.fn();

    vi.stubGlobal('navigator', { serviceWorker: fake.container });
    vi.stubGlobal('location', { reload });

    const stop = watchForUpdates();

    await settle();

    toaster.toasts[0]!.action!.run();

    expect(waiting.messages).toEqual([{ type: 'culina:activate' }]);
    // Not yet: reloading before the new worker is in control would serve the
    // old build again and lose the update.
    expect(reload).not.toHaveBeenCalled();

    fake.takeControl();

    expect(reload).toHaveBeenCalledTimes(1);

    stop();
    vi.unstubAllGlobals();
  });
});
