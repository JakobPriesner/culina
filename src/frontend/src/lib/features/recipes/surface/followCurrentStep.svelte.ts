import { tick } from 'svelte';

/** What following needs to know, read afresh each time so it stays reactive. */
interface Source {
  /** The list the steps are in, once it exists. */
  readonly list: () => HTMLOListElement | undefined;
  readonly cooking: () => boolean;
  readonly current: () => number;
}

/**
 * Waits for the steps to finish changing size.
 *
 * The step becoming current grows and the one leaving it shrinks, both
 * animated, so the geometry a step has the instant it becomes current is not
 * the geometry it has a blink later. Measuring the first scrolls to where the
 * step was rather than where it lands. `getAnimations` is missing in jsdom,
 * and an animation that never ends must not stall the scroll forever.
 */
const resized = async (list: HTMLElement) => {
  await tick();

  await Promise.race([
    Promise.allSettled((list.getAnimations?.({ subtree: true }) ?? []).map((one) => one.finished)),
    new Promise((done) => setTimeout(done, 400))
  ]);
};

/**
 * Brings a step into the part of the screen nothing is parked over.
 *
 * That part is the viewport less the `scroll-margin` on `.step`, which the
 * cook page sizes to its own controls — so "fits" means fits between the
 * header and the controls, not half a viewport that the controls may be
 * standing in. A step that fits is centred there, with the next one already
 * showing underneath; a longer one starts at the top, where it is read from.
 *
 * `followed` is a move, which always brings the step over. Otherwise the
 * page is left alone while the step is readable: it starts in the clear,
 * and ends there too when it can.
 */
const reveal = (step: Element | undefined, followed: boolean) => {
  // `scrollIntoView` is missing in jsdom, and moving the screen is a
  // courtesy rather than behaviour the page depends on.
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
    // A move is followed smoothly, so it reads as the page following rather
    // than jumping — unless the person has said they do not want things
    // moving. A rescue is not something to watch.
    behavior:
      followed && !window.matchMedia?.('(prefers-reduced-motion: reduce)').matches
        ? 'smooth'
        : 'auto'
  });
};

/**
 * The step being cooked comes to the middle of the screen, and stays there.
 *
 * Steps are as long as they need to be, so after two or three of them the
 * one being cooked is wherever the previous one left it — often under the
 * controls at the bottom, or off the top. Moving it to the middle means the
 * answer to "what am I doing now" is always in the same place, and the step
 * after it is already visible underneath.
 *
 * Only while cooking: reading is scrolled by the person doing it, and a page
 * that moves under a reader's thumb is a page fighting them. Call it while a
 * component is being set up, as it registers effects.
 */
export function followCurrentStep(source: Source) {
  /**
   * Whether the page has arrived.
   *
   * Arriving is not a move. A page that scrolls itself the moment it opens has
   * taken the cook somewhere they did not ask to go — unless it opened with the
   * step they are cooking under the controls, where it cannot be read. The
   * first run only rescues a step like that; every move after it follows.
   */
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

  /**
   * The same rescue when the screen changes shape.
   *
   * A phone turned on its side reflows every step, and the one being cooked
   * lands wherever the reflow puts it. Width only: the address bar sliding
   * away as the cook scrolls changes the height, and answering that would pull
   * the page back out from under their thumb.
   */
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
