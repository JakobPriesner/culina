interface Selection {
  selected: string | undefined;
  underline?: boolean;
}

/** Move only the decoration, preserving labels, hit targets and focus. */
export function selectionIndicator(host: HTMLElement, options: Selection) {
  const indicator = document.createElement('span');
  indicator.className = 'selection-indicator';
  indicator.setAttribute('aria-hidden', 'true');
  indicator.hidden = true;
  host.append(indicator);
  let previous = '';

  function place(animate: boolean) {
    const target = options.selected
      ? host.querySelector<HTMLElement>(`[data-selection="${CSS.escape(options.selected)}"]`)
      : null;
    const to = target?.getBoundingClientRect();
    if (!to?.width || !to.height) {
      indicator.hidden = true;
      previous = '';
      return;
    }
    const parent = host.getBoundingClientRect();
    const height = options.underline ? 2 : to.height;
    const top =
      (options.underline ? to.bottom - height : to.top) -
      parent.top +
      host.scrollTop -
      host.clientTop;
    const left = to.left - parent.left + host.scrollLeft - host.clientLeft;
    const geometry = `${left} ${top} ${to.width} ${height}`;
    if (geometry === previous) return;

    // Geometry changes on resize settle directly. CSS transitions handle
    // interrupted selection changes from the current painted position.
    if (!animate) indicator.style.transition = 'none';
    indicator.hidden = false;
    indicator.style.transform = `translate(${left}px, ${top}px)`;
    indicator.style.width = `${to.width}px`;
    indicator.style.height = `${height}px`;
    previous = geometry;
    host.setAttribute('data-indicator-ready', '');
    if (!animate) {
      indicator.getBoundingClientRect();
      indicator.style.transition = '';
    }
  }

  function update(next: Selection) {
    options = next;
    indicator.classList.toggle('underline', !!options.underline);
    place(true);
  }
  const observer = new ResizeObserver(() => place(false));
  observer.observe(host);
  host.querySelectorAll('[data-selection]').forEach((item) => observer.observe(item));
  indicator.classList.toggle('underline', !!options.underline);
  place(false);
  return {
    update,
    destroy() {
      observer.disconnect();
      indicator.remove();
      host.removeAttribute('data-indicator-ready');
    }
  };
}
