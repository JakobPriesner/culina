/**
 * Stops the page behind an overlay from scrolling.
 *
 * Counted rather than boolean: a sheet that opens a confirmation on top of
 * itself would otherwise unlock the page when the inner one closes, and the
 * content behind would start scrolling under a dialog that is still open.
 */
let depth = 0;
let restore = '';

export function lockScroll(): void {
  if (depth === 0 && globalThis.document) {
    restore = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
  }

  depth += 1;
}

export function unlockScroll(): void {
  depth = Math.max(0, depth - 1);

  if (depth === 0 && globalThis.document) {
    document.body.style.overflow = restore;
  }
}
