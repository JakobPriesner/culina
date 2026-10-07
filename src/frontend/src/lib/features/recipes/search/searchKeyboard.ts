import type { Completion } from '../types';
import type { SearchSession } from './createSearchSession.svelte';

interface Context {
  readonly session: SearchSession;
  /** What the field could complete to, in the order it is drawn. */
  readonly completions: readonly Completion[];
  /** The listbox's id, which the options' ids are built from. */
  readonly id: string;
  /** Enter on a field with nothing highlighted: search for what was typed. */
  readonly submit: () => void;
}

/** What the keys do in the search field, so focus never has to leave it. */
export function searchKeydown(
  event: KeyboardEvent,
  { session, completions, id, submit }: Context
): void {
  const { options, typed, tags } = session;

  if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
    event.preventDefault();

    if (options.length > 0) {
      const step = event.key === 'ArrowDown' ? 1 : -1;
      session.highlighted = (session.highlighted + step + options.length) % options.length;
      document
        .getElementById(`${id}-${options[session.highlighted]!.key}`)
        ?.scrollIntoView({ block: 'nearest' });
    }
  } else if (event.key === 'Enter') {
    event.preventDefault();

    const option = options[session.highlighted];

    if (option) {
      session.activate(option, event.metaKey || event.ctrlKey);
    } else {
      submit();
    }
  } else if (
    event.key === 'Tab' &&
    !event.shiftKey &&
    typed.trim().length > 0 &&
    completions.length > 0
  ) {
    // The highlighted completion, or the first one. Only when there is one
    // to take: otherwise Tab leaves the field as it always does.
    event.preventDefault();

    const option = options[session.highlighted];

    session.accept(option?.kind === 'completion' ? option.completion : completions[0]!);
  } else if (event.key === 'Backspace' && typed.length === 0 && tags.length > 0) {
    // Nothing left to delete in the field, so the chip beside it goes —
    // what deleting one more character looks like it should do.
    event.preventDefault();
    session.removeTag(tags.at(-1)!.slug);
  } else if (event.key === 'Escape' && typed.length > 0) {
    // Clear first, close second — the same as every other search field here.
    event.preventDefault();
    session.set('');
  }
}
