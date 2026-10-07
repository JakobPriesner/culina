/**
 * Whether anybody can see Olli: on screen, in a visible tab. While work goes
 * on, a change in either starts the movement over (or stops it) so the
 * blinks and beats are not spent on nobody. Call while the component is
 * initialising: it sets up its own effects.
 */
export function createAudience({
  element,
  active,
  rest,
  restart
}: {
  element: () => Element | undefined;
  /** Whether movement goes on for as long as the work does. */
  active: () => boolean;
  /** Cancels every pending beat. */
  rest: () => void;
  /** Starts the movement over; only called when somebody is watching. */
  restart: () => void;
}) {
  // A plain field, not state: nothing renders it, and reading state from the
  // effects below would make them wake themselves up.
  let visible = true;
  const watched = () => visible && !document.hidden;
  const refresh = () => {
    rest();
    if (watched()) restart();
  };

  $effect(() => {
    const target = element();

    if (!target || typeof IntersectionObserver === 'undefined') {
      return;
    }

    const observer = new IntersectionObserver(([entry]) => {
      const wasVisible = visible;
      visible = entry?.isIntersecting ?? true;
      if (active() && visible !== wasVisible) refresh();
    });

    observer.observe(target);

    return () => observer.disconnect();
  });

  $effect(() => {
    if (!active()) return;
    document.addEventListener('visibilitychange', refresh);
    return () => document.removeEventListener('visibilitychange', refresh);
  });

  return { watched };
}
