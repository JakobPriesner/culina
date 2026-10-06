/** Keeps the page stationary behind nested modal surfaces. */
let depth = 0;
let restoreRoot = '';
let restoreBody = '';
let restorePadding = '';
let compensated = false;

export function lockScroll(): void {
  if (!globalThis.document) return;
  if (depth === 0) {
    const root = document.documentElement;
    const body = document.body;
    restoreRoot = root.style.overflow;
    restoreBody = body.style.overflow;
    restorePadding = body.style.paddingInlineEnd;
    const gap = Math.max(0, window.innerWidth - root.clientWidth);
    compensated = gap > 0 && !globalThis.CSS?.supports?.('scrollbar-gutter', 'stable');
    if (compensated) {
      const padding = parseFloat(getComputedStyle(body).paddingInlineEnd) || 0;
      body.style.paddingInlineEnd = `${padding + gap}px`;
    }
    // Lock the actual document scroller as well as the body. Modern browsers
    // retain the root's stable gutter; older ones use the measured padding.
    root.style.overflow = 'hidden';
    body.style.overflow = 'hidden';
  }
  depth += 1;
}

export function unlockScroll(): void {
  if (depth === 0 || !globalThis.document) return;
  depth -= 1;
  if (depth === 0) {
    document.documentElement.style.overflow = restoreRoot;
    document.body.style.overflow = restoreBody;
    if (compensated) document.body.style.paddingInlineEnd = restorePadding;
    compensated = false;
  }
}
