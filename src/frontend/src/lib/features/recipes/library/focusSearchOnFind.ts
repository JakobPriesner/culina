import { searchOverlay } from '$features/recipes/search/overlayState.svelte';

/**
 * ⌘F / Ctrl-F focuses the recipe search field right on the page rather than
 * letting the browser open its own in-page search.
 */
export function focusSearchOnFind(event: KeyboardEvent, searchId: string) {
  if (event.defaultPrevented) {
    return;
  }

  const isFind =
    (event.metaKey || event.ctrlKey) &&
    !event.altKey &&
    !event.shiftKey &&
    event.key.toLowerCase() === 'f';

  if (!isFind || searchOverlay.open || document.querySelector('dialog[open]')) {
    return;
  }

  const field = document.getElementById(searchId) as HTMLInputElement | null;

  if (field && !field.disabled) {
    event.preventDefault();
    field.focus();
    field.select();
  }
}
