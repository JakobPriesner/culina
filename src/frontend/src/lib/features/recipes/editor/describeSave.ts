import { ErrorCodes, type AppError } from '$api';
import { changedElsewhere } from '$features/recipes/stores/recipes.svelte';
import { m } from '$shell/i18n';

import type { SaveTone } from './SaveState.svelte';

const unreachable = (failure: AppError) =>
  failure.code === ErrorCodes.offline || failure.code === ErrorCodes.timeout;

export interface SaveFacts {
  readonly failure: AppError | null;
  readonly saving: boolean;
  readonly saved: boolean;
  readonly recovered: boolean;
  readonly unsent: boolean;
}

/**
 * The status word beside the title, in priority order: a conflict is never masked, unsent work never reads
 * "Saved", and an unreachable server reads as "kept here" rather than a failure.
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
