/**
 * The search field a page has of its own, such as the library's. While one is on screen the header's search
 * button leads to it rather than opening the overlay: a long list then needs one tap back to its search.
 */
class PageSearch {
  /** Brings the field into view, focused; null while no page offers one. */
  reveal = $state<(() => void) | null>(null);
}

export const pageSearch = new PageSearch();
