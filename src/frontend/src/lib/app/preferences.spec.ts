import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { m } from './i18n';
import { preferences } from './preferences.svelte';
import { storageKey } from './appearance';

/*
 * The store's job is to make a choice survive three things that can each fail
 * independently: the paint, the reload, and the network.
 */
const stored = () => JSON.parse(localStorage.getItem(storageKey) ?? '{}') as Record<string, string>;

/** The network, stood in for — assertions read this rather than the global. */
let send: ReturnType<typeof vi.fn>;

function networkAnswers(reply: () => Promise<Response>) {
  send = vi.fn(reply);
  vi.stubGlobal('fetch', send);
}

const accepted = () =>
  Promise.resolve(
    new Response('{}', { status: 200, headers: { 'Content-Type': 'application/json' } })
  );

beforeEach(() => {
  localStorage.clear();
  preferences.reset();
  networkAnswers(accepted);
});

describe('choosing an appearance', () => {
  it('paints the document immediately', () => {
    preferences.setMode('dark');

    expect(document.documentElement.dataset['mode']).toBe('dark');
  });

  it('stores it under the key the boot script reads', () => {
    preferences.setMode('dark');

    expect(stored()).toEqual({ theme: 'warm-paper', mode: 'dark' });
  });

  it('stores the choice, not the resolved value, so following the device keeps working', () => {
    preferences.setMode('system');

    expect(stored()['mode']).toBe('system');
  });
});

describe('choosing a language', () => {
  /** What the device says it reads, in order of preference. */
  const deviceReads = (...languages: string[]) =>
    vi.spyOn(navigator, 'languages', 'get').mockReturnValue(languages);

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('follows the device until someone chooses otherwise', () => {
    deviceReads('de-AT', 'en');
    preferences.reset();

    expect(preferences.localeChoice).toBe('system');
    expect(preferences.locale).toBe('de');
    expect(m['locale.label']()).toBe('Sprache');
  });

  it('falls back to English for a device that reads nothing Culina speaks', () => {
    deviceReads('fr-FR');
    preferences.reset();

    expect(preferences.locale).toBe('en');
  });

  it('lets a chosen language win over the device', () => {
    deviceReads('de-AT');
    preferences.reset();

    preferences.setLocale('en');

    expect(preferences.locale).toBe('en');
    expect(document.documentElement.lang).toBe('en');
    expect(m['locale.label']()).toBe('Language');
  });

  it('remembers `system` itself, so a later change on the device is still followed', () => {
    preferences.setLocale('de');
    preferences.setLocale('system');

    expect(localStorage.getItem('culina.locale')).toBe('system');
  });

  it('picks the remembered choice up again on the next visit', () => {
    localStorage.setItem('culina.locale', 'de');

    const stop = preferences.start();

    expect(preferences.localeChoice).toBe('de');
    expect(preferences.locale).toBe('de');
    stop();
  });

  it('follows the device while the app is open', () => {
    const languages = deviceReads('en-GB');
    const stop = preferences.start();

    languages.mockReturnValue(['de-DE']);
    globalThis.dispatchEvent(new Event('languagechange'));

    expect(preferences.locale).toBe('de');
    expect(document.documentElement.lang).toBe('de');
    stop();
  });

  it('ignores something that is not a choice', () => {
    preferences.setLocale('klingon');

    expect(preferences.localeChoice).toBe('system');
  });
});

describe('a signed-out person', () => {
  it('is not asked to sync anything', async () => {
    preferences.setMode('dark');

    await vi.waitFor(() => expect(send).not.toHaveBeenCalled());
  });
});

describe('a signed-in person', () => {
  beforeEach(() => {
    preferences.adopt({ locale: 'en', theme: 'warm-paper', mode: 'light' }, { signedIn: true });
  });

  it('sends the whole preference set, because the endpoint replaces it', async () => {
    preferences.setMode('dark');

    await vi.waitFor(() => expect(send).toHaveBeenCalled());

    const sent = send.mock.calls[0]?.[0] as Request;
    const body: unknown = await sent.json();

    expect(body).toEqual({
      locale: 'en',
      theme: 'warm-paper',
      mode: 'dark',
      measurementSystem: 'metric'
    });
  });

  it('keeps the change and flags it when the network refuses', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(() => Promise.reject(new TypeError('Failed to fetch')))
    );

    preferences.setMode('dark');

    await vi.waitFor(() => expect(preferences.unsynced).toBe(true));
    expect(preferences.mode).toBe('dark');
    expect(stored()['mode']).toBe('dark');
  });

  it('sends `system` as the choice, not the language it resolved to', async () => {
    preferences.setLocale('system');

    await vi.waitFor(() => expect(send).toHaveBeenCalled());

    const sent = send.mock.calls[0]?.[0] as Request;
    const body = (await sent.json()) as Record<string, string>;

    expect(body['locale']).toBe('system');
  });

  it('takes the server value on boot, because another device may be newer', () => {
    preferences.adopt({ mode: 'dark', theme: 'warm-paper' }, { signedIn: true });

    expect(preferences.mode).toBe('dark');
    expect(document.documentElement.dataset['mode']).toBe('dark');
  });
});
