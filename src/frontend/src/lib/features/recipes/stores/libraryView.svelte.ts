import { registerStore } from '$shell/stores';

/** Keep the library's place through a recipe round trip, scoped to the household. */
class LibraryView {
  query = $state('');
  quick = $state(false);
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
    this.#householdId = null;
  }
}

export const libraryView = new LibraryView();
registerStore(() => libraryView.reset());
