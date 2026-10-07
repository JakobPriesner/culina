import { ErrorCodes, type AppError } from '$api';
import { changedElsewhere } from '$features/recipes/stores/recipes.svelte';
import { m } from '$shell/i18n';

import type { SaveTone } from './SaveState.svelte';

/** A failure that means the server was not reached, rather than refused. */
const unreachable = (failure: AppError) =>
  failure.code === ErrorCodes.offline || failure.code === ErrorCodes.timeout;

export interface SaveFacts {
  readonly failure: AppError | null;
  readonly saving: boolean;
  readonly saved: boolean;
  /** The editor opened onto work a previous visit had not saved. */
  readonly recovered: boolean;
  /** What is on screen exists only on this device. */
  readonly unsent: boolean;
}

/**
 * What the small word beside the title says.
 *
 * In the order that matters. A conflict is never masked by anything
 * reassuring; work that has not reached the server never reads as "Saved";
 * and a lost connection reads as where the work is rather than as a failure,
 * because the work is not lost — it is on this device, and saying "Could not
 * save" about it is both alarming and untrue.
 *
 * At rest it says that the recipe saves itself, which is the one question an
 * editor with no Save button owes an answer to before anything has happened.
 */
export function describeSave(facts: SaveFacts): { tone: SaveTone; text: string } {
  const { failure } = facts;

  if (failure && changedElsewhere(failure)) {
    return { tone: 'conflict', text: m['editor.conflict.short']() };
  }

  if (facts.saving) {
    return { tone: 'saving', text: m['editor.saving']() };
  }

  if (failure && !unreachable(failure)) {
    return { tone: 'failed', text: m['editor.saveFailed']() };
  }

  if (facts.recovered) {
    return { tone: 'local', text: m['editor.recovered']() };
  }

  if (facts.unsent) {
    return { tone: 'local', text: m['editor.keptHere']() };
  }

  return facts.saved
    ? { tone: 'saved', text: m['editor.saved']() }
    : { tone: 'idle', text: m['editor.savesItself']() };
}
