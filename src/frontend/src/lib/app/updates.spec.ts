import { beforeEach, describe, expect, it, vi } from 'vitest';

import { busy } from './busy.svelte';
import { toaster } from './toaster.svelte';
import { update, watchForUpdates } from './updates.svelte';

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

  /** The build it answers with when asked, as the real worker does. */
  constructor(readonly version = 'v2') {}

  postMessage(message: unknown, transfer?: Transferable[] | StructuredSerializeOptions): void {
    if ((message as { type?: string }).type === 'culina:version') {
      const ports = Array.isArray(transfer) ? transfer : [];

      (ports[0] as MessagePort | undefined)?.postMessage(this.version);

      return;
    }

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

/** Lets the registration and the worker's answer settle before anything is asserted. */
const settle = () => new Promise((resolve) => setTimeout(resolve, 20));

/** A fresh page: nothing held, nothing on screen — but the device remembers. */
const reopen = () => {
  toaster.reset();
  update.reset();
};

describe('watching for a new version', () => {
  beforeEach(() => {
    reopen();
    busy.reset();
    localStorage.clear();
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
    // Brief: a prompt that never left sat over the bottom of every screen. The
    // offer itself stays, in settings, until it is taken.
    expect(toast!.durationMs).toBeGreaterThan(0);
    expect(update.ready).toBe(true);
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

describe('mentioning a version', () => {
  beforeEach(() => {
    reopen();
    busy.reset();
    localStorage.clear();
  });

  it('offers it once, however often the browser reports it', async () => {
    const waiting = new FakeWorker();
    const fake = fakeServiceWorker({ waiting, controlled: true });

    vi.stubGlobal('navigator', { serviceWorker: fake.container });

    const first = watchForUpdates();
    const second = watchForUpdates();

    await settle();

    // Two identical prompts at once is what this used to produce.
    expect(toaster.toasts).toHaveLength(1);

    first();
    second();
    vi.unstubAllGlobals();
  });

  it('does not mention the same version again on the next visit, but keeps offering it', async () => {
    const fake = fakeServiceWorker({ waiting: new FakeWorker('v2'), controlled: true });

    vi.stubGlobal('navigator', { serviceWorker: fake.container });

    const stop = watchForUpdates();

    await settle();
    expect(toaster.toasts).toHaveLength(1);

    stop();
    reopen();

    const again = watchForUpdates();

    await settle();

    expect(toaster.toasts).toHaveLength(0);
    expect(update.ready).toBe(true);

    again();
    vi.unstubAllGlobals();
  });

  it('mentions the next version, which is news', async () => {
    const older = fakeServiceWorker({ waiting: new FakeWorker('v2'), controlled: true });

    vi.stubGlobal('navigator', { serviceWorker: older.container });

    const stop = watchForUpdates();

    await settle();
    stop();
    reopen();

    const newer = fakeServiceWorker({ waiting: new FakeWorker('v3'), controlled: true });

    vi.stubGlobal('navigator', { serviceWorker: newer.container });

    const again = watchForUpdates();

    await settle();

    expect(toaster.toasts).toHaveLength(1);

    again();
    vi.unstubAllGlobals();
  });
});

describe('a new version arriving at a bad moment', () => {
  beforeEach(() => {
    reopen();
    busy.reset();
    localStorage.clear();
  });

  it('says nothing while somebody is cooking or editing', async () => {
    const fake = fakeServiceWorker({ waiting: new FakeWorker(), controlled: true });
    const release = busy.hold();

    vi.stubGlobal('navigator', { serviceWorker: fake.container });

    const stop = watchForUpdates();

    await settle();

    // An offer is still an interruption, and "there is a new version" is never
    // worth reading with your hands in a bowl.
    expect(toaster.toasts).toHaveLength(0);

    release();
    stop();
    vi.unstubAllGlobals();
  });

  it('mentions it at the next safe moment, rather than forgetting', async () => {
    const fake = fakeServiceWorker({ waiting: new FakeWorker(), controlled: true });
    const release = busy.hold();

    vi.stubGlobal('navigator', { serviceWorker: fake.container });

    const stop = watchForUpdates();

    await settle();

    release();
    await settle();

    expect(toaster.toasts).toHaveLength(1);

    stop();
    vi.unstubAllGlobals();
  });
});
