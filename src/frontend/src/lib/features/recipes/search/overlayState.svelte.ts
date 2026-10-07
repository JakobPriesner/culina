/** Whether the search is open over the current page. A state rather than a `/search` route, which would leave the chosen page, pollute history and make searching while planning cost the plan. */
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
