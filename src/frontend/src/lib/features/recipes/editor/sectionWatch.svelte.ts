/** Which form section is being read, and a `jump` that scrolls to another. Call during component init. */
export function createSectionWatch(sections: () => readonly { id: string }[]) {
  let seenSection = $state<string | null>(null);

  // The first section until the observer reports: avoids a flicker with nothing marked.
  const current = $derived(seenSection ?? sections()[0]?.id ?? null);

  /** Phone bar shows the recipe name only once the title field has scrolled away. */
  const away = $derived(current !== null && current !== sections()[0]?.id);

  // Observed, not scroll-measured, to stay cheap on slow devices; the band is the upper third of the viewport.
  $effect(() => {
    const watched = sections()
      .map((section) => document.getElementById(section.id))
      .filter((element): element is HTMLElement => element !== null);

    if (watched.length === 0) {
      return;
    }

    // Plain record: not reactive, and the observer reports only changes, so earlier state must be remembered.
    const seen: Record<string, boolean> = {};

    const observer = new IntersectionObserver(
      (entries) => {
        for (const entry of entries) {
          seen[entry.target.id] = entry.isIntersecting;
        }

        // First in recipe order, not in the observer's (arbitrary) report order.
        seenSection = sections().find((section) => seen[section.id])?.id ?? seenSection;
      },
      { rootMargin: '-15% 0px -70% 0px' }
    );

    for (const element of watched) {
      observer.observe(element);
    }

    return () => observer.disconnect();
  });

  /** Smooth-scrolls to a section and moves focus to its heading like native hash navigation, without a history entry per jump. */
  function jump(event: MouseEvent, id: string) {
    const target = document.getElementById(id);

    // No element yet, or a modified click that opens elsewhere on purpose.
    if (!target || event.metaKey || event.ctrlKey || event.shiftKey || event.button !== 0) {
      return;
    }

    event.preventDefault();
    seenSection = id;

    const still = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches;

    // Missing in jsdom.
    target.scrollIntoView?.({ block: 'start', behavior: still ? 'auto' : 'smooth' });
    target.focus({ preventScroll: true });
  }

  return {
    get current() {
      return current;
    },
    get away() {
      return away;
    },
    jump
  };
}
