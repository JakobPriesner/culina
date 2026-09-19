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

/**
 * Where the shell offers to write a new recipe.
 *
 * One test: would what the button makes belong to what is on screen? A new
 * recipe joins the library, so the library offers it. Nothing else does.
 *
 * - A cookbook is a shelf, and a new recipe does not land on it. That page has
 *   its own control for putting recipes on the shelf, and two "add" buttons
 *   that add different things is the kind of screen people learn to distrust.
 * - The week and the shopping list are other domains entirely.
 * - One recipe's page is about that recipe; every other control on it acts on
 *   the recipe, and this one would be the exception, in the loudest position.
 * - The editor, the importer and the cook screen are the worst of all: there
 *   the button navigates away from work that has not been saved.
 *
 * It was on all of them. The library is one tap away from every one of them,
 * on a bar that is already on screen.
 */
export const offersNewRecipe = (pathname: string): boolean => pathname === library;

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
