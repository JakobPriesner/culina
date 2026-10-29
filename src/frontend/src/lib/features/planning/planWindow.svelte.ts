import { preferences } from '$shell/preferences.svelte';

import { dateToday, plusDays, weekStart } from './days';
import { daysShown, planView, type PlanView } from './planView.svelte';
import { daysTitle, mondayOf, rangeOf, weekOffsetOf, weekTitle } from './weekDates';

/**
 * The days the plan has on screen (docs/woche-design.md §10): the coming days from any day, stepped a day at a time,
 * or a Monday-to-Sunday week, stepped a week at a time.
 */
export class PlanWindow {
  readonly today = dateToday();

  view = $state<PlanView>('days');
  /** The first day on screen: a Monday in the week view, any day in the days view. */
  start = $state('');
  /** The day a list line (or a meal on the list) leads back to, shown and marked. */
  sought = $state<string | null>(null);

  readonly count = $derived(this.view === 'week' ? 7 : daysShown);
  readonly end = $derived(plusDays(this.start, this.count - 1));
  /** Weeks from this one to the week the window starts in. */
  readonly offset = $derived(weekOffsetOf(this.start));
  readonly atToday = $derived(this.start === this.#todayStart());

  /** What is shown: this week, next week, the coming days, or from which day. */
  readonly title = $derived(
    this.view === 'week' ? weekTitle(this.offset) : daysTitle(this.start, preferences.locale)
  );
  readonly range = $derived(rangeOf(this.start, preferences.locale, this.count));

  /** Where "Rezept planen" plans for: today when it is on screen, else the first day shown. */
  readonly firstOpenDay = $derived(
    this.start <= this.today && this.today <= this.end ? this.today : this.start
  );

  /** The days of this week already over, folded into one line until asked for: on a Saturday, Monday is history. */
  readonly foldsEarlier = $derived(
    this.view === 'week' && this.start === mondayOf(0) && !planView.earlierShown(this.start)
  );

  /**
   * Opens on `week` when one was asked for (`?week=1`, as "See the week" does after planning ahead). Otherwise on the
   * coming days on a phone, where the week's first days would be a screen of meals already eaten, and on the week
   * beside the list, which it is the shopping of; whichever was chosen last on this device wins over both.
   */
  constructor(opening: { week: number | null; listBeside: boolean }) {
    if (opening.week !== null) {
      this.view = 'week';
      this.start = mondayOf(opening.week);

      return;
    }

    this.view = planView.chosen ?? (opening.listBeside ? 'week' : 'days');
    this.start = this.#todayStart();
  }

  /** Looks the other way, at the week holding what was shown; the days from today when that week is this one. */
  choose = (next: PlanView) => {
    planView.choose(next);

    const monday = weekStart(this.start);

    if (next === 'week') {
      this.start = monday;
    } else if (monday === mondayOf(0)) {
      this.start = this.today;
    }

    this.view = next;
  };

  step = (direction: -1 | 1) => {
    this.start = plusDays(this.start, direction * (this.view === 'week' ? 7 : 1));
  };

  toToday = () => {
    this.start = this.#todayStart();
  };

  /** Shows the days holding `date`. */
  jump = (date: string) => {
    this.start = this.view === 'week' ? weekStart(date) : date;
  };

  /** Shows the days holding `date`, and marks it. */
  seek = (date: string) => {
    this.sought = date;
    this.jump(date);
  };

  showEarlier = () => {
    planView.showEarlier(this.start);
  };

  #todayStart() {
    return this.view === 'week' ? mondayOf(0) : this.today;
  }
}
