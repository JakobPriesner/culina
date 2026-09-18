import { resolve } from '$app/paths';

import { m } from './i18n';

/** The five stable workflows, in the same order on desktop and mobile. */
export type DestinationIcon = 'recipes' | 'cookbooks' | 'plan' | 'shopping' | 'settings';

export interface Destination {
  readonly href: string;
  /** Named rather than derived from the href, which a base path can change. */
  readonly icon: DestinationIcon;
  readonly label: () => string;
  /** Matches child routes so the containing destination remains selected. */
  readonly match: (pathname: string) => boolean;
}

const startsWith = (prefix: string) => (pathname: string) =>
  pathname === prefix || pathname.startsWith(`${prefix}/`);

/** One peer navigation model, rendered directly at every viewport size. */
export const destinations: readonly Destination[] = [
  {
    href: resolve('/(app)'),
    icon: 'recipes',
    label: m['nav.recipes'],
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
    label: m['nav.plan'],
    match: startsWith('/plan')
  },
  {
    href: resolve('/(app)/shopping'),
    icon: 'shopping',
    label: m['nav.shopping'],
    match: startsWith('/shopping')
  },
  {
    href: resolve('/(app)/me'),
    icon: 'settings',
    label: m['nav.me'],
    match: startsWith('/me')
  }
];
