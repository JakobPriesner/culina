/**
 * Which planned meal's lines the list is showing on request (docs/woche-design.md §5.3): "Auf der Liste" on a card
 * leads here, and the meal's lines are marked for a moment where they stand; an unplanned meal's lines stay marked
 * while the list asks what becomes of them.
 */
class LineMarks {
  #entryId = $state<string | null>(null);
  #timer: ReturnType<typeof setTimeout> | undefined;

  /** The plan entry whose lines are marked, if any. */
  get entryId(): string | null {
    return this.#entryId;
  }

  show(entryId: string): void {
    clearTimeout(this.#timer);
    this.#entryId = entryId;
    this.#timer = setTimeout(() => (this.#entryId = null), 4000);
  }

  /** Marked until `release`: while a question about these lines is open. */
  hold(entryId: string): void {
    clearTimeout(this.#timer);
    this.#entryId = entryId;
  }

  release(entryId: string): void {
    if (this.#entryId === entryId) this.#entryId = null;
  }
}

export const lineMarks = new LineMarks();
