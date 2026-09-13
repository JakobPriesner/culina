/**
 * Whether now is a bad moment to interrupt.
 *
 * Two things in Culina must not be interrupted: cooking, because somebody is
 * standing at a hob with their hands full, and editing, because there is
 * unsaved text on screen. Anything that wants to offer a reload waits for both
 * to be over.
 *
 * Deliberately a count rather than a flag: two components can hold it at once —
 * the cook screen and an editor open in another tab of the same app — and the
 * moment is only safe again when the last of them lets go.
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
