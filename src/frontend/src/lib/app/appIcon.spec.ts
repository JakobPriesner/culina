import { fireEvent, screen } from '@testing-library/svelte';
import { beforeEach, describe, expect, it } from 'vitest';

import { renderWithProviders } from '$lib/test/render';

import AppIconChoice from './AppIconChoice.svelte';
import { appIcon } from './appIcon.svelte';
import { appIconLinks, appIconStorageKey, defaultAppIcon, parseAppIcon } from './appIcons';

/*
 * The choice has to reach the links a browser reads when Culina is installed,
 * and survive a reload — and a stored value we did not write must not point
 * those links at files that do not exist.
 */
const href = (selector: string) => document.head.querySelector(selector)?.getAttribute('href');

beforeEach(() => {
  localStorage.clear();
  // The links app.html ships with.
  document.head.innerHTML = `
    <link rel="icon" href="/favicon.ico" sizes="any" />
    <link rel="icon" href="/icon.svg" type="image/svg+xml" />
    <link rel="apple-touch-icon" href="/apple-touch-icon.png" />
    <link rel="manifest" href="/manifest.webmanifest" />`;
});

describe('reading a stored app icon', () => {
  it('accepts what we wrote', () => {
    expect(parseAppIcon('ink')).toBe('ink');
  });

  it.each([
    ['nothing stored', null],
    ['an icon that no longer exists', 'terracotta'],
    ['a path', '../../etc']
  ])('falls back to the default: %s', (_, stored) => {
    expect(parseAppIcon(stored)).toBe(defaultAppIcon);
  });
});

describe("the default icon's files", () => {
  it('are the ones at the root, which browsers and existing installs ask for unprompted', () => {
    expect(appIconLinks(defaultAppIcon)).toEqual({
      manifest: '/manifest.webmanifest',
      svg: '/icon.svg',
      ico: '/favicon.ico',
      appleTouch: '/apple-touch-icon.png'
    });
  });
});

describe('choosing an app icon', () => {
  it('points every link the browser installs from at its files', () => {
    appIcon.choose('saffron');

    expect(href('link[rel="manifest"]')).toBe('/icons/saffron/manifest.webmanifest');
    expect(href('link[type="image/svg+xml"]')).toBe('/icons/saffron/icon.svg');
    expect(href('link[sizes="any"]')).toBe('/icons/saffron/favicon.ico');
    expect(href('link[rel="apple-touch-icon"]')).toBe('/icons/saffron/apple-touch-icon.png');
  });

  it('is remembered on this device, and picked up on the next start', () => {
    appIcon.choose('basil');
    document.head
      .querySelector('link[rel="manifest"]')!
      .setAttribute('href', '/manifest.webmanifest');

    appIcon.start();

    expect(localStorage.getItem(appIconStorageKey)).toBe('basil');
    expect(appIcon.current).toBe('basil');
    expect(href('link[rel="manifest"]')).toBe('/icons/basil/manifest.webmanifest');
  });

  it('leaves the links alone when nothing was ever chosen', () => {
    appIcon.start();

    expect(appIcon.current).toBe(defaultAppIcon);
    expect(href('link[rel="manifest"]')).toBe('/manifest.webmanifest');
  });
});

describe('the app icon picker', () => {
  it('offers every icon by name, with the one in use chosen', () => {
    appIcon.start();
    renderWithProviders(AppIconChoice);

    expect(screen.getAllByRole('radio')).toHaveLength(5);
    expect(screen.getByRole('radio', { name: 'Cocotte' })).toBeChecked();
  });

  it('switches the icon when another is picked', async () => {
    appIcon.start();
    renderWithProviders(AppIconChoice);

    await fireEvent.click(screen.getByRole('radio', { name: 'Ink' }));

    expect(screen.getByRole('radio', { name: 'Ink' })).toBeChecked();
    expect(href('link[rel="manifest"]')).toBe('/icons/ink/manifest.webmanifest');
  });
});
