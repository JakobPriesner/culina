/**
 * Whether the search is open, over whatever page is on screen.
 *
 * Search is a state the app can be in rather than a place it goes: a `/search`
 * route would navigate away from the page somebody chose, put results in the
 * history where the back button turns them into archaeology, and make
 * finding a recipe while planning Thursday cost Thursday. So the shell holds
 * this flag, anything can raise it, and closing it leaves the page beneath
 * exactly as it was.
 */
class SearchOverlay {
  #open = $state(false);

  get open(): boolean {
    return this.#open;
  }

  show(): void {
    this.#open = true;
  }

  hide(): void {
    this.#open = false;
  }
}

export const searchOverlay = new SearchOverlay();
