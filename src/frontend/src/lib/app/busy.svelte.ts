/**
 * Whether now is a bad moment to interrupt: while cooking or with unsaved edits, anything offering a reload waits.
 * A count, not a flag: holders can overlap (cook screen, editor in another tab) and the last must let go.
 */
class Busy {
  #holders = $state(0);

  get interruptible(): boolean {
    return this.#holders === 0;
  }

  /** Marks now as a bad moment. Call the returned function when it is over. */
  hold(): () => void {
    this.#holders += 1;

    let released = false;

    return () => {
      if (released) {
        return;
      }

      released = true;
      this.#holders -= 1;
    };
  }

  reset(): void {
    this.#holders = 0;
  }
}

export const busy = new Busy();
