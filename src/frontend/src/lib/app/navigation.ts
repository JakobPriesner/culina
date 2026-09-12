import { resolve } from '$app/paths';

import { m } from './i18n';

/**
 * The three places the app goes.
 *
 * Three, and the list is closed. A fourth would mean the shell is being used to
 * hide something that did not earn a place — settings live inside Me, and
 * anything rarer than daily belongs behind one of these rather than beside it.
 */
export type DestinationIcon = 'recipes' | 'shopping' | 'me';

export interface Destination {
  readonly href: string;
  /** Named rather than derived from the href, which a base path can change. */
  readonly icon: DestinationIcon;
  readonly label: () => string;
  /** Matches child routes too, so a recipe keeps Recipes lit. */
  readonly match: (pathname: string) => boolean;
}

const startsWith = (prefix: string) => (pathname: string) =>
  pathname === prefix || pathname.startsWith(`${prefix}/`);

export const destinations: readonly Destination[] = [
  {
    href: resolve('/(app)'),
    icon: 'recipes',
    label: m['nav.recipes'],
    match: (path) => path === '/' || startsWith('/recipes')(path)
  },
  {
    href: resolve('/(app)/shopping'),
    icon: 'shopping',
    label: m['nav.shopping'],
    match: startsWith('/shopping')
  },
  {
    href: resolve('/(app)/me'),
    icon: 'me',
    label: m['nav.me'],
    match: startsWith('/me')
  }
];
