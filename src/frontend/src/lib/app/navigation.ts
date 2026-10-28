import { resolve } from '$app/paths';

import { m } from './i18n';
import type { WocheView } from './woche.svelte';

/** The stable workflows; the bar shows them in the same order on desktop and mobile. */
export type DestinationIcon = 'recipes' | 'cookbooks' | 'plan' | 'shopping' | 'diary';

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

/** Settings are not a workflow: the account menu leads there, and lights up while they are on screen. */
export const inSettings = startsWith('/me');

/** Whether search is offered: everywhere but the cook screen, where a search box invites losing the place. */
export const offersSearch = (pathname: string): boolean => !pathname.endsWith('/cook');

/** Every place, as search offers them; the bar folds the plan and the list into Woche (`barDestinations`). */
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
  }
];

const diaryDestination: Destination = {
  href: resolve('/(app)/diary'),
  icon: 'diary',
  label: m['diary.title'],
  match: startsWith('/diary')
};

/**
 * The bar, the same at every width (docs/woche-design.md): Woche holds the plan and the list, leads to whichever of
 * its two views was used last and stays lit on both. The diary has the fourth place, for everyone: while it is off,
 * its page offers to switch it on, so the bar never changes shape when someone does.
 */
export function barDestinations(view: WocheView): Destination[] {
  const [recipes, cookbooks] = destinations;
  const week: Destination = {
    href: view === 'list' ? resolve('/(app)/shopping') : resolve('/(app)/plan'),
    icon: 'plan',
    label: m['nav.plan'],
    match: (path) => startsWith('/plan')(path) || startsWith('/shopping')(path)
  };

  return [recipes!, cookbooks!, week, diaryDestination];
}
