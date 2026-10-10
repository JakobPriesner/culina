import type { Attachment } from 'svelte/attachments';

/**
 * Runs something when an element nears the viewport (how lists load their next page); `reach` may
 * fire repeatedly, so keep it cheap.
 */
export function whenVisible(reach: () => void, margin = '600px'): Attachment {
  return (element) => {
    // No IntersectionObserver in jsdom; every supported browser has it.
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

/**
 * Reports whether an element is still in view below `top` px of the viewport, and false once it has
 * scrolled up past that line (not when it is merely below the fold), e.g. to condense a sticky bar.
 */
export function visibleBelow(top: number, report: (visible: boolean) => void): Attachment {
  return (element) => {
    if (typeof IntersectionObserver === 'undefined') {
      return;
    }

    const observer = new IntersectionObserver(
      (entries) => {
        for (const entry of entries) {
          report(entry.isIntersecting || entry.boundingClientRect.top >= top);
        }
      },
      { rootMargin: `-${top}px 0px 0px 0px` }
    );

    observer.observe(element);

    return () => observer.disconnect();
  };
}
