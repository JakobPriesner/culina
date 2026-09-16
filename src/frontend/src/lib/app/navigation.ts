import { resolve } from '$app/paths';

import { m } from './i18n';

/** Global areas and their collection destinations stay separate at every viewport size. */
export type DestinationIcon = 'recipes' | 'cookbooks' | 'plan' | 'shopping' | 'me';

export interface Destination {
  readonly href: string;
  /** Named rather than derived from the href, which a base path can change. */
  readonly icon: DestinationIcon;
  readonly label: () => string;
  /** Matches child routes too, so a recipe or a cookbook keeps Recipes lit. */
  readonly match: (pathname: string) => boolean;
}

const startsWith = (prefix: string) => (pathname: string) =>
  pathname === prefix || pathname.startsWith(`${prefix}/`);

export const destinations: readonly Destination[] = [
  {
    href: resolve('/(app)'),
    icon: 'recipes',
    label: m['nav.library'],
    // Cookbooks and the week live behind this one rather than beside it, so
    // they light it: a destination that goes dark when you follow a link out
    // of it makes the shell look like it lost you.
    match: (path) =>
      path === '/' ||
      startsWith('/recipes')(path) ||
      startsWith('/cookbooks')(path) ||
      startsWith('/plan')(path)
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

/** Shared by the desktop sidebar and the compact library sheet. */
export const libraryDestinations: readonly (Destination & { separate?: boolean })[] = [
  {
    href: resolve('/(app)'),
    icon: 'recipes',
    label: m['library.recipes'],
    match: (path) => path === '/' || startsWith('/recipes')(path)
  },
  {
    href: resolve('/(app)/cookbooks'),
    icon: 'cookbooks',
    label: m['library.cookbooks'],
    match: startsWith('/cookbooks')
  },
  {
    href: resolve('/(app)/plan'),
    icon: 'plan',
    label: m['plan.open'],
    match: startsWith('/plan'),
    separate: true
  }
];
