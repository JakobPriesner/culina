/** Keep the mobile cooking action visible until its place in the recipe arrives. */
export function dockCookingAction(anchor: HTMLElement) {
  const footer = anchor.querySelector('footer')!;
  const compact = window.matchMedia('(width < 64rem)');
  const viewport = window.visualViewport;
  let frame = 0;

  function update() {
    frame = 0;

    if (!compact.matches) {
      footer.classList.remove('docked');
      anchor.style.removeProperty('--action-height');
      return;
    }

    const height = footer.getBoundingClientRect().height;
    // Reserve the same space whether the footer is fixed or in the document.
    anchor.style.setProperty('--action-height', `${height}px`);
    const place = anchor.getBoundingClientRect();
    const inset = parseFloat(getComputedStyle(anchor).getPropertyValue('--bottom-inset')) || 0;
    const visibleBottom = (viewport?.offsetTop ?? 0) + (viewport?.height ?? window.innerHeight);
    const docked = place.top + height > visibleBottom - inset;

    anchor.style.setProperty('--action-left', `${place.left}px`);
    anchor.style.setProperty('--action-width', `${place.width}px`);
    anchor.style.setProperty('--action-top', `${visibleBottom - inset - height}px`);
    footer.classList.toggle('docked', docked);
  }

  function schedule() {
    if (!frame) frame = requestAnimationFrame(update);
  }

  const observer = new ResizeObserver(schedule);
  observer.observe(anchor);
  observer.observe(footer);
  // Navigation, the cooking bar and import progress can change the bottom inset.
  for (const bar of anchor.closest('.shell')?.querySelectorAll('.bar, .dock') ?? []) {
    observer.observe(bar);
  }
  window.addEventListener('scroll', schedule, { passive: true });
  window.addEventListener('resize', schedule);
  viewport?.addEventListener('scroll', schedule, { passive: true });
  viewport?.addEventListener('resize', schedule);
  compact.addEventListener('change', schedule);
  update();

  return {
    destroy() {
      cancelAnimationFrame(frame);
      observer.disconnect();
      window.removeEventListener('scroll', schedule);
      window.removeEventListener('resize', schedule);
      viewport?.removeEventListener('scroll', schedule);
      viewport?.removeEventListener('resize', schedule);
      compact.removeEventListener('change', schedule);
    }
  };
}
