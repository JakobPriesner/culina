import { readDevice, writeDevice } from '$shell/deviceStorage';

/**
 * How the plan is looked at (docs/woche-design.md §10): the coming days from one day on, stepped a day at a time, or
 * a Monday-to-Sunday week, which is also what the shopping list fills from. No day view (one day is too little to plan
 * with) and no month to plan in (the month picker jumps to a date instead).
 */
export type PlanView = 'days' | 'week';

/** Today and the three after it: what is cooked soon, and the most a phone shows without a second screen of days. */
export const daysShown = 4;

const viewKey = 'culina.plan.view';

class PlanViewChoice {
  #chosen = $state<PlanView | null>(readChoice());

  /** The view last chosen on this device; null until someone chooses, when the screen's width decides. */
  get chosen(): PlanView | null {
    return this.#chosen;
  }

  choose(view: PlanView): void {
    this.#chosen = view;
    writeDevice(viewKey, view);
  }
}

function readChoice(): PlanView | null {
  const stored = readDevice(viewKey);

  return stored === 'days' || stored === 'week' ? stored : null;
}

export const planView = new PlanViewChoice();
