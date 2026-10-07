import { tick } from 'svelte';

interface Source {
  readonly list: () => HTMLOListElement | undefined;
  readonly cooking: () => boolean;
  readonly current: () => number;
}

/**
 * Waits for step grow/shrink animations to settle, so we scroll to where the step lands.
 * `getAnimations` is missing in jsdom, and the 400ms cap stops a never-ending animation stalling the scroll.
 */
const resized = async (list: HTMLElement) => {
  await tick();

  await Promise.race([
    Promise.allSettled((list.getAnimations?.({ subtree: true }) ?? []).map((one) => one.finished)),
    new Promise((done) => setTimeout(done, 400))
  ]);
};

/**
 * Scrolls a step into the area between header and controls (the viewport less `.step`'s `scroll-margin`).
 * `followed` always scrolls; otherwise the page is left alone while the step is readable.
 */
const reveal = (step: Element | undefined, followed: boolean) => {
  // `scrollIntoView` is missing in jsdom.
  if (!(step instanceof HTMLElement) || !step.scrollIntoView) {
    return;
  }

  const style = getComputedStyle(step);
  const top = parseFloat(style.scrollMarginTop) || 0;
  const bottom = window.innerHeight - (parseFloat(style.scrollMarginBottom) || 0);
  const box = step.getBoundingClientRect();
  const fits = box.height <= bottom - top;
  const readable = box.top >= top && box.top < bottom && (!fits || box.bottom <= bottom);

  if (!followed && readable) {
    return;
  }

  step.scrollIntoView({
    block: fits ? 'center' : 'start',
    // Smooth only for a followed move, and never under reduced motion.
    behavior:
      followed && !window.matchMedia?.('(prefers-reduced-motion: reduce)').matches
        ? 'smooth'
        : 'auto'
  });
};

/**
 * Keeps the step being cooked centred on screen; only while cooking, never while reading.
 * Call during component setup, as it registers effects.
 */
export function followCurrentStep(source: Source) {
  // The first run only rescues an unreadable step; scrolling a freshly opened page would be unasked.
  let arrived = false;

  $effect(() => {
    const index = source.current();
    const list = source.list();

    if (!source.cooking() || !list) {
      return;
    }

    const moved = arrived;
    arrived = true;

    let current = true;

    void resized(list).then(() => {
      if (!current) {
        return;
      }

      const step = list.children[index];

      reveal(step, moved);

      if (moved) {
        step?.querySelector<HTMLElement>('[aria-current="step"]')?.focus({ preventScroll: true });
      }
    });

    return () => {
      current = false;
    };
  });

  // Rescue on rotation. Width only: the address bar changing height while scrolling must not pull the page back.
  $effect(() => {
    const list = source.list();

    if (!source.cooking() || !list) {
      return;
    }

    let width = window.innerWidth;

    const onresize = () => {
      if (window.innerWidth !== width) {
        width = window.innerWidth;
        void resized(list).then(() => reveal(list.children[source.current()], false));
      }
    };

    window.addEventListener('resize', onresize);

    return () => window.removeEventListener('resize', onresize);
  });
}
