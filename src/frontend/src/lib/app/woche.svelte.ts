import { MediaQuery } from 'svelte/reactivity';

import { resolve } from '$app/paths';

import { readDevice, writeDevice } from './deviceStorage';

/** Woche's two views: the week's plan and the household's list. */
export type WocheView = 'plan' | 'list';

/**
 * From 70rem, a tablet on its side, the plan shows the list beside the week, live; below, the week needs the whole
 * width (at 1024px beside a pane its cards wrapped on touch), so Plan | Liste flips between the two. PlanHeader's
 * stylesheet repeats the breakpoint, as CSS cannot read it from here.
 */
export const listBeside = new MediaQuery('(min-width: 70rem)', false);

/** The page each view lives on. */
export function wocheHref(view: WocheView) {
  return view === 'list' ? resolve('/(app)/week/shopping') : resolve('/(app)/week/plan');
}

const viewKey = 'culina.woche.view';

/**
 * Woche holds the week's plan and the household's shopping list as one place (docs/woche-design.md): each keeps its
 * own page, so links, Back and the list in a shop work as ever. The tab returns to the view used last on this
 * device, so in a shop the list is one tap away; flipping with the switch lands each view where it was left.
 */
class Woche {
  #view = $state<WocheView>(readDevice(viewKey) === 'list' ? 'list' : 'plan');

  /** Plain, not state: where each view was scrolled to when the switch left it, for this visit to the app. */
  #scrolled: Partial<Record<WocheView, number>> = {};
  #returning: WocheView | null = null;

  get view(): WocheView {
    return this.#view;
  }

  /** The view the switch is going back to, which then opens where it was rather than at today. */
  get returningTo(): WocheView | null {
    return this.#returning;
  }

  /** Remembered on every visit to either view. */
  visit(view: WocheView): void {
    if (this.#view === view) return;
    this.#view = view;
    writeDevice(viewKey, view);
  }

  /** The switch leaves `from` at `y` for `to`; whether `to` has a place of its own to land on. */
  leave(from: WocheView, to: WocheView, y: number): boolean {
    this.#scrolled[from] = y;
    this.#returning = this.#scrolled[to] === undefined ? null : to;

    return this.#returning !== null;
  }

  /** Arriving through the switch: where to scroll. Still `returningTo` until `arrived`, so the week does not jump to today meanwhile. */
  arrive(view: WocheView): number | null {
    return this.#returning === view ? (this.#scrolled[view] ?? null) : null;
  }

  arrived(): void {
    this.#returning = null;
  }
}

export const woche = new Woche();
