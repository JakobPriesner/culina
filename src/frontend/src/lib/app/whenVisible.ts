import type { Attachment } from 'svelte/attachments';

/**
 * Runs something when an element comes near the viewport.
 *
 * This is how a list loads its next page: the placeholders at the end of what
 * has been read are watched, and reaching them is the request. A scroll
 * handler would do the same thing by asking the layout for a position on every
 * frame, which is work the browser can do for free and better.
 *
 * `margin` is deliberately generous — the next page is asked for while the end
 * of the list is still below the fold, so the rows are usually there by the
 * time anyone could have read that far, and the skeletons are a glimpse rather
 * than a wait.
 *
 * `reach` may be called more than once: an element can leave and re-enter, and
 * several watched elements can enter together. Whatever it calls has to be
 * cheap to call again.
 */
export function whenVisible(reach: () => void, margin = '600px'): Attachment {
  return (element) => {
    // Nothing to watch with. Every browser Culina supports has this; jsdom
    // does not, and a test that never scrolls has nothing to observe anyway.
    if (typeof IntersectionObserver === 'undefined') {
      return;
    }

    const observer = new IntersectionObserver(
      (entries) => {
        if (entries.some((entry) => entry.isIntersecting)) {
          reach();
        }
      },
      { rootMargin: margin }
    );

    observer.observe(element);

    return () => observer.disconnect();
  };
}
