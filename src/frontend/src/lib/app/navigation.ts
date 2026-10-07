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

const library = resolve('/(app)');

/** Where the shell offers a new recipe: only where what it makes belongs to what is on screen, i.e. the library. Not on cookbooks (own add-to-shelf control), the week, shopping or one recipe, nor in the editor, importer or cook screen, where it would leave unsaved work. */
export const offersNewRecipe = (pathname: string): boolean => pathname === library;

/** Whether search is offered: everywhere but the cook screen, where a search box invites losing the place. */
export const offersSearch = (pathname: string): boolean => !pathname.endsWith('/cook');

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
