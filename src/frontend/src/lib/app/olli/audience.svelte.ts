/**
 * Tracks whether Olli is on screen in a visible tab and restarts or rests the movement on change.
 * Call during component init, as it sets up effects.
 */
export function createAudience({
  element,
  active,
  rest,
  restart
}: {
  element: () => Element | undefined;
  active: () => boolean;
  rest: () => void;
  /** Only called while watched. */
  restart: () => void;
}) {
  // Not state: nothing renders it, and a tracked read would make the effects retrigger themselves.
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
