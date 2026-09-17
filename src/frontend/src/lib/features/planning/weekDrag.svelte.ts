/**
 * Picking a planned meal up, and putting it down on another day.
 *
 * Pointer events rather than the HTML5 drag-and-drop API, which never fires on
 * iOS. One implementation for the mouse and the finger is the whole point: two
 * would be two ideas of where a card lands, and only one of them would be the
 * one anybody tested.
 *
 * What differs between them is only how a drag *starts*, because the two
 * gestures mean different things at rest. A mouse press that then moves is
 * unambiguous, so it takes six pixels. A finger press that then moves is how
 * the week is scrolled, so it takes a held moment first — which is also what
 * every iOS list that can be rearranged asks for.
 */

/** Far enough that it was not a click with a shaky hand. */
const threshold = 6;

/** Long enough to be deliberate, short enough not to feel stuck. */
const holdMs = 350;

/** A finger that travels this far before the hold is done was scrolling. */
const scrollSlop = 10;

/** How close to an edge starts scrolling, and how fast it goes at the very edge. */
const edge = 72;
const edgeSpeed = 18;

/** The meal being carried. */
export interface Held {
  readonly entryId: string;
  readonly title: string;
  /** The day it was lifted from, so a drop back onto it changes nothing. */
  readonly from: string;
  /** What it measured on the page, so the thing under the pointer is its size. */
  readonly width: number;
}

/** The gap the pointer is currently over. */
export interface Landing {
  readonly date: string;
  /** Counted with the held meal still in place, which is how it is drawn. */
  readonly position: number;
}

/** What the page does when a drag ends over a day. */
export type OnDrop = (held: Held, landing: Landing) => void;

/**
 * Which day and gap a point is over.
 *
 * Read off the page rather than from a registry the page keeps in step: the
 * days are already in the document, and a registry is a second description of
 * the layout that is wrong the first time something reflows.
 */
const landingAt = (x: number, y: number): Landing | null => {
  const day = document.elementFromPoint(x, y)?.closest<HTMLElement>('[data-plan-day]');

  if (!day?.dataset.planDay) {
    return null;
  }

  const meals = [...day.querySelectorAll<HTMLElement>('[data-plan-meal]')];

  // The gap above every card whose middle the pointer has passed. Counting
  // middles rather than edges means the card under the pointer is always the
  // one being displaced, with no dead band between two of them.
  const position = meals.filter((meal) => {
    const box = meal.getBoundingClientRect();

    return y > box.top + box.height / 2;
  }).length;

  return { date: day.dataset.planDay, position };
};

/**
 * The state of a drag, and the listeners that drive it.
 *
 * One instance per week view. It owns no data about meals beyond what it is
 * handed on the way down — where a meal ends up is the store's business, and a
 * controller that also wrote to the store would be a second place the week is
 * changed.
 */
export class WeekDrag {
  #held = $state<Held | null>(null);
  #landing = $state<Landing | null>(null);
  #at = $state({ x: 0, y: 0 });

  /** True only once the gesture has become a drag, never merely a press. */
  get held(): Held | null {
    return this.#held;
  }

  /** Where it would land, or null when the pointer is off every day. */
  get landing(): Landing | null {
    return this.#landing;
  }

  /** Where to draw the thing under the pointer. */
  get at(): { x: number; y: number } {
    return this.#at;
  }

  #hold: ReturnType<typeof setTimeout> | undefined;
  #start = { x: 0, y: 0 };
  #pending: Held | null = null;
  #scrolling = 0;
  #onDrop: OnDrop = () => {};

  /**
   * Begins watching a press. Nothing is lifted yet.
   *
   * @param event The press.
   * @param held What would be carried if it becomes a drag.
   * @param onDrop What to do when it is let go over a day.
   */
  press(event: PointerEvent, held: Omit<Held, 'width'>, onDrop: OnDrop): void {
    if (event.button !== 0) {
      return;
    }

    const source = (event.currentTarget as HTMLElement).closest<HTMLElement>('[data-plan-meal]');

    if (!source) {
      return;
    }

    this.#onDrop = onDrop;
    this.#start = { x: event.clientX, y: event.clientY };
    this.#pending = { ...held, width: source.getBoundingClientRect().width };

    window.addEventListener('pointermove', this.#move);
    window.addEventListener('pointerup', this.#release);
    window.addEventListener('pointercancel', this.#release);

    if (event.pointerType === 'touch') {
      this.#hold = setTimeout(() => this.#lift(this.#start.x, this.#start.y), holdMs);
    }
  }

  #move = (event: PointerEvent) => {
    const travelled = Math.hypot(event.clientX - this.#start.x, event.clientY - this.#start.y);

    if (!this.#held) {
      if (this.#hold) {
        // Still waiting out a long press. A finger that has set off is
        // scrolling the week, so the press stops being a candidate.
        if (travelled > scrollSlop) {
          this.#cancelHold();
          this.#detach();
        }

        return;
      }

      if (travelled < threshold) {
        return;
      }

      this.#lift(event.clientX, event.clientY);

      if (!this.#held) {
        return;
      }
    }

    this.#at = { x: event.clientX, y: event.clientY };
    this.#aim(event.clientX, event.clientY);
    this.#autoScroll(event.clientY);
  };

  /**
   * Takes aim, keeping the last day it was over when it is over none.
   *
   * Forgiving on purpose. A card carried to the bottom of the screen is often
   * a finger hanging just past the last day, and scrolling moves the days out
   * from under a pointer that has not itself moved. Letting go there means the
   * day you were last on, not nothing — and if that was not what you meant,
   * Undo is on the screen it lands on.
   */
  #aim(x: number, y: number): void {
    this.#landing = landingAt(x, y) ?? this.#landing;
  }

  #lift(x: number, y: number): void {
    this.#cancelHold();

    if (!this.#pending) {
      return;
    }

    this.#held = this.#pending;
    this.#at = { x, y };
    this.#aim(x, y);

    // Non-passive, and only while something is actually in the air: this is
    // what stops iOS scrolling the page under a finger that is carrying a card.
    // `touch-action: none` on the card would do it too, and would also make the
    // week unscrollable everywhere a card is, which on a phone is everywhere.
    document.addEventListener('touchmove', preventDefault, { passive: false });
    document.addEventListener('contextmenu', preventDefault);
  }

  #release = () => {
    const held = this.#held;
    const landing = this.#landing;

    this.#cancelHold();
    this.#detach();

    if (!held) {
      return;
    }

    // The press became a drag, so the click it is about to produce is not one
    // anybody meant — the card is a link to the recipe, and letting go over
    // Thursday would otherwise also open it. Swallowed in the capture phase,
    // before the link sees it.
    //
    // Disarmed on the way out of this task rather than left waiting for a click
    // that may never come: a drag that ends over a different element than it
    // started on produces no click at all, and a listener still armed then is
    // one that eats the next thing the person presses, anywhere on the page.
    window.addEventListener('click', swallow, { capture: true, once: true });
    setTimeout(() => window.removeEventListener('click', swallow, { capture: true }));

    if (landing) {
      this.#onDrop(held, landing);
    }
  };

  /**
   * Scrolls when the pointer is near an edge.
   *
   * Seven days never fit on a phone, so without this the only reachable days
   * are the ones that happen to be on screen when the card is picked up.
   */
  #autoScroll(y: number): void {
    const above = edge - y;
    const below = y - (window.innerHeight - edge);
    const by = above > 0 ? -above : below > 0 ? below : 0;

    cancelAnimationFrame(this.#scrolling);

    if (by === 0) {
      return;
    }

    const step = Math.sign(by) * Math.min(edgeSpeed, (Math.abs(by) / edge) * edgeSpeed);

    const run = () => {
      window.scrollBy(0, step);
      this.#aim(this.#at.x, this.#at.y);
      this.#scrolling = requestAnimationFrame(run);
    };

    this.#scrolling = requestAnimationFrame(run);
  }

  #cancelHold(): void {
    clearTimeout(this.#hold);
    this.#hold = undefined;
  }

  #detach(): void {
    window.removeEventListener('pointermove', this.#move);
    window.removeEventListener('pointerup', this.#release);
    window.removeEventListener('pointercancel', this.#release);
    document.removeEventListener('touchmove', preventDefault);
    document.removeEventListener('contextmenu', preventDefault);

    cancelAnimationFrame(this.#scrolling);

    this.#held = null;
    this.#landing = null;
    this.#pending = null;
  }

  /** Lets go of everything, for a component that is going away mid-drag. */
  stop(): void {
    this.#cancelHold();
    this.#detach();
  }
}

const preventDefault = (event: Event) => event.preventDefault();

const swallow = (event: Event) => {
  event.preventDefault();
  event.stopPropagation();
};
