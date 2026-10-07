import type { Completion } from '../types';
import type { SearchSession } from './createSearchSession.svelte';

interface Context {
  readonly session: SearchSession;
  readonly completions: readonly Completion[];
  /** The listbox's id, prefix of the option ids. */
  readonly id: string;
  /** Enter on a field with nothing highlighted: search for what was typed. */
  readonly submit: () => void;
}

/** Search field keys, so focus never leaves the field. */
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
    // Only intercept Tab when there is a completion to take.
    event.preventDefault();

    const option = options[session.highlighted];

    session.accept(option?.kind === 'completion' ? option.completion : completions[0]!);
  } else if (event.key === 'Backspace' && typed.length === 0 && tags.length > 0) {
    // Empty field: Backspace removes the last chip.
    event.preventDefault();
    session.removeTag(tags.at(-1)!.slug);
  } else if (event.key === 'Escape' && typed.length > 0) {
    // Clear first, close second.
    event.preventDefault();
    session.set('');
  }
}
