import { searchOverlay } from '$features/recipes/search/overlayState.svelte';

/** Makes ⌘F / Ctrl-F focus the page's search field instead of the browser's find bar. */
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
