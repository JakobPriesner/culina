interface Options {
  enabled: boolean;
  step: number;
  suspended: boolean;
  onstop: () => void;
}

/** Read a long step hands-free, without shrinking the cook's text. */
export function autoScrollStep(root: HTMLElement, initial: Options) {
  let options = initial;
  const reduced = window.matchMedia('(prefers-reduced-motion: reduce)');
  const viewport = window.visualViewport;
  let frame = 0;
  let timer: ReturnType<typeof setTimeout> | undefined;
  let last = 0;
  let waitUntil = 0;
  let direction = 1;
  let restart = true;
  let position: number | undefined;

  function cancel() {
    cancelAnimationFrame(frame);
    clearTimeout(timer);
    timer = undefined;
    frame = 0;
    last = 0;
    position = undefined;
  }

  function schedule(delay = 0) {
    if (
      options.enabled &&
      !options.suspended &&
      !document.hidden &&
      !frame &&
      timer === undefined
    ) {
      if (delay > 0) {
        timer = setTimeout(() => {
          timer = undefined;
          schedule();
        }, delay);
      } else {
        frame = requestAnimationFrame(read);
      }
    }
  }

  function read(now: number) {
    frame = 0;
    if (!options.enabled || options.suspended || document.hidden) return;

    // RecipeSurface follows each step after its resize animation. Give that
    // move time to settle before taking over, including native smooth scroll.
    if (!last) waitUntil = now + 800;
    const elapsed = Math.min(now - (last || now), 100);
    last = now;
    if (now < waitUntil) {
      schedule(waitUntil - now);
      return;
    }

    const step = root.querySelector<HTMLElement>('.step.current');
    if (!step) {
      schedule(1000);
      return;
    }
    const style = getComputedStyle(step);
    const box = step.getBoundingClientRect();
    const top = (viewport?.offsetTop ?? 0) + (parseFloat(style.scrollMarginTop) || 0);
    const bottom =
      (viewport?.offsetTop ?? 0) +
      (viewport?.height ?? window.innerHeight) -
      (parseFloat(style.scrollMarginBottom) || 0);
    const room = bottom - top;
    // Short steps stay still. Leave a fitting step where RecipeSurface put it.
    if (room <= 0 || box.height <= room + 1) {
      position = undefined;
      schedule(1000);
      return;
    }

    const maximum = Math.max(0, document.documentElement.scrollHeight - window.innerHeight);
    const start = Math.min(maximum, Math.max(0, window.scrollY + box.top - top));
    const end = Math.min(maximum, Math.max(start, window.scrollY + box.bottom - bottom));
    const dwell = reduced.matches ? 8000 : 5000;

    if (restart) {
      restart = false;
      direction = 1;
      position = start;
      waitUntil = now + dwell;
    } else {
      position = Math.min(end, Math.max(start, position ?? window.scrollY));
      // Keep fractional pixels ourselves: reading scrollY back every frame
      // loses subpixel movement in browsers that round scroll positions.
      const distance = reduced.matches ? room * 0.7 : (elapsed / 1000) * 20;
      position = Math.min(end, Math.max(start, position + direction * distance));
      if ((direction > 0 && position >= end) || (direction < 0 && position <= start)) {
        direction *= -1;
        waitUntil = now + dwell;
      } else if (reduced.matches) {
        // Reduced motion reads overlapping portions with an instant jump,
        // rather than making the text move continuously under the cook's eye.
        waitUntil = now + dwell;
      }
    }
    window.scrollTo({ top: position, behavior: 'instant' });
    schedule(Math.max(0, waitUntil - now));
  }

  function stop() {
    if (!options.enabled || options.suspended) return;
    cancel();
    options = { ...options, enabled: false };
    options.onstop();
  }

  function touch(event: Event) {
    // Controls remain usable, including the explicit Stop button. Touching
    // the recipe itself hands scrolling back to the cook before the gesture.
    if (event.target instanceof Element && event.target.closest('.controls')) return;
    stop();
  }

  function key(event: KeyboardEvent) {
    // Space activates a focused control. Stopping here would turn Stop into
    // Start before the button's click fires on keyup.
    if (
      event.key === ' ' &&
      event.target instanceof Element &&
      event.target.closest('button, input, textarea, select, [contenteditable="true"]')
    ) {
      return;
    }
    if (['ArrowUp', 'ArrowDown', 'PageUp', 'PageDown', 'Home', 'End', ' '].includes(event.key)) {
      stop();
    }
  }

  function resume() {
    cancel();
    schedule();
  }

  window.addEventListener('wheel', stop, { passive: true });
  window.addEventListener('touchstart', touch, { passive: true });
  window.addEventListener('pointerdown', touch, { passive: true });
  window.addEventListener('keydown', key);
  window.addEventListener('resize', resume);
  viewport?.addEventListener('resize', resume);
  document.addEventListener('visibilitychange', resume);
  reduced.addEventListener('change', resume);
  schedule();

  return {
    update(next: Options) {
      if (next.step !== options.step || (next.enabled && !options.enabled)) {
        restart = true;
      }
      options = next;
      cancel();
      schedule();
    },
    destroy() {
      cancel();
      window.removeEventListener('wheel', stop);
      window.removeEventListener('touchstart', touch);
      window.removeEventListener('pointerdown', touch);
      window.removeEventListener('keydown', key);
      window.removeEventListener('resize', resume);
      viewport?.removeEventListener('resize', resume);
      document.removeEventListener('visibilitychange', resume);
      reduced.removeEventListener('change', resume);
    }
  };
}
