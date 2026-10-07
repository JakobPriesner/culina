/**
 * Picking a planned meal up and dropping it on another day, with pointer events (HTML5 drag-and-drop never fires on iOS).
 * A mouse starts dragging after 6px; a finger needs a held moment first, because press-and-move scrolls the week.
 */

/** Pixels a mouse must move before it is a drag rather than a shaky click. */
const threshold = 6;

/** Hold time before a touch becomes a drag. */
const holdMs = 350;

/** A finger travelling this far before the hold completes was scrolling. */
const scrollSlop = 10;

/** Distance from an edge that starts auto-scroll, and the top speed at the edge. */
const edge = 72;
const edgeSpeed = 18;

export interface Held {
  readonly entryId: string;
  readonly title: string;
  /** Source day; dropping back onto it changes nothing. */
  readonly from: string;
  /** Measured card size, for the floating copy under the pointer. */
  readonly width: number;
}

export interface Landing {
  readonly date: string;
  /** Counted with the held meal still in place. */
  readonly position: number;
}

export type OnDrop = (held: Held, landing: Landing) => void;

/** The day and gap under a point, read from the DOM; a registry would go stale on reflow. */
const landingAt = (x: number, y: number): Landing | null => {
  const day = document.elementFromPoint(x, y)?.closest<HTMLElement>('[data-plan-day]');

  if (!day?.dataset.planDay) {
    return null;
  }

  const meals = [...day.querySelectorAll<HTMLElement>('[data-plan-meal]')];

  // Gap above every card whose middle the pointer has passed; middles rather than edges leave no dead band.
  const position = meals.filter((meal) => {
    const box = meal.getBoundingClientRect();

    return y > box.top + box.height / 2;
  }).length;

  return { date: day.dataset.planDay, position };
};

/** Drag state and listeners, one per week view; where a meal ends up is the store's business. */
export class WeekDrag {
  #held = $state<Held | null>(null);
  #landing = $state<Landing | null>(null);
  #at = $state({ x: 0, y: 0 });

  /** Set only once a press has become a drag. */
  get held(): Held | null {
    return this.#held;
  }

  get landing(): Landing | null {
    return this.#landing;
  }

  get at(): { x: number; y: number } {
    return this.#at;
  }

  #hold: ReturnType<typeof setTimeout> | undefined;
  #start = { x: 0, y: 0 };
  #pending: Held | null = null;
  #scrolling = 0;
  #onDrop: OnDrop = () => {};

  /** Starts watching a press; nothing is lifted until it becomes a drag. */
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
        // Still in the long-press wait: a finger that set off is scrolling.
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

  /** Keeps the last day when over none: a finger hanging past the last day, or scrolling, should still drop there. */
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

    // Non-passive, and only while carrying: stops iOS scrolling under the finger.
    // `touch-action: none` on the card would make the week unscrollable wherever a card is.
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

    // Swallow the trailing click in capture so the drop does not also open the card's recipe link.
    // Disarmed after this task: a drag ending on another element yields no click, and a still-armed listener would eat the next press.
    window.addEventListener('click', swallow, { capture: true, once: true });
    setTimeout(() => window.removeEventListener('click', swallow, { capture: true }));

    if (landing) {
      this.#onDrop(held, landing);
    }
  };

  /** Scrolls near an edge; seven days never fit on a phone. */
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

  /** Releases everything, for a component unmounting mid-drag. */
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
