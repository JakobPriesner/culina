import { registerStore } from '$shell/stores';

/**
 * How the library is ordered.
 *
 * Two, and only two. "Recently updated" answers "what did I change last?",
 * which is a question nobody has while hungry — it is here because it is the
 * order the app has always had and taking it away would be taking something
 * from whoever relied on it.
 */
export type LibraryOrder = 'suggested' | 'recent';

/** Keep the library's place through a recipe round trip, scoped to the household. */
class LibraryView {
  query = $state('');
  quick = $state(false);

  /**
   * Which order, once somebody has chosen one.
   *
   * Null means nobody has, and the page decides: suggested as soon as the
   * ranking has something true to say about this kitchen, and the old order
   * before that. Stored as a choice rather than as a boolean default, so that
   * picking "recently updated" sticks even on a day the ranking would have
   * offered to take over.
   */
  order = $state<LibraryOrder | null>(null);

  #householdId: string | null = null;

  forHousehold(id: string): void {
    if (this.#householdId !== id) {
      this.reset();
      this.#householdId = id;
    }
  }

  reset(): void {
    this.query = '';
    this.quick = false;
    this.order = null;
    this.#householdId = null;
  }
}

export const libraryView = new LibraryView();
registerStore(() => libraryView.reset());
