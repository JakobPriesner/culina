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
