/**
 * Which section of a long form is being read, and how to get to another.
 *
 * Call it while a component initialises: it watches the document for as long
 * as that component lives.
 */
export function createSectionWatch(sections: () => readonly { id: string }[]) {
  /** Which section the observer has last seen, or none before it has run. */
  let seenSection = $state<string | null>(null);

  /**
   * Which section the rail marks.
   *
   * The first until the observer says otherwise: the page opens at the top, and
   * a rail that marks nothing for the first frame flickers.
   */
  const current = $derived(seenSection ?? sections()[0]?.id ?? null);

  /**
   * Whether the bar has to say which recipe this is.
   *
   * On a phone it does not, while the title field is on screen — the name is
   * already there, in the field, in the editorial face, at four times the size.
   * It appears as soon as that field has scrolled away and the bar is the only
   * thing left that could say it.
   */
  const away = $derived(current !== null && current !== sections()[0]?.id);

  /**
   * Watched rather than computed from the scroll position.
   *
   * A scroll handler measuring four elements on every frame is the version that
   * makes the page stutter on the one device that cannot afford it. The band is
   * the upper third of the viewport: a heading that has reached it is the one
   * being read, and the section it belongs to is the one to mark.
   */
  $effect(() => {
    const watched = sections()
      .map((section) => document.getElementById(section.id))
      .filter((element): element is HTMLElement => element !== null);

    if (watched.length === 0) {
      return;
    }

    // A plain record rather than a Set, because nothing here is reactive: the
    // observer reports only what changed, so the ones it did not mention have
    // to be remembered from the call before.
    const seen: Record<string, boolean> = {};

    const observer = new IntersectionObserver(
      (entries) => {
        for (const entry of entries) {
          seen[entry.target.id] = entry.isIntersecting;
        }

        // The first in the recipe's own order, not the first the observer
        // happened to report: entries arrive in whatever order they changed.
        seenSection = sections().find((section) => seen[section.id])?.id ?? seenSection;
      },
      { rootMargin: '-15% 0px -70% 0px' }
    );

    for (const element of watched) {
      observer.observe(element);
    }

    return () => observer.disconnect();
  });

  /**
   * Goes to a section the way the browser would, and moves focus with it.
   *
   * The link keeps its `href`, so it can be opened in a new tab and read as a
   * link — but the jump is taken over here rather than left to the document,
   * because a hash in the address bar of an editor is a history entry per
   * glance at the ingredients, and because `scroll-behavior` is a property of
   * the whole page and this is the only screen that wants it.
   *
   * Focus moves to the heading, which is what the browser's own hash
   * navigation does and the reason it is worth reproducing: a keyboard user who
   * jumps to the steps must land in the steps, not back at the top of the form.
   */
  function jump(event: MouseEvent, id: string) {
    const target = document.getElementById(id);

    // No element yet, or a click the browser owns — a modified click is
    // somebody opening it somewhere else on purpose.
    if (!target || event.metaKey || event.ctrlKey || event.shiftKey || event.button !== 0) {
      return;
    }

    event.preventDefault();
    seenSection = id;

    const still = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches;

    // Missing in jsdom, where moving the screen is not what is under test.
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
