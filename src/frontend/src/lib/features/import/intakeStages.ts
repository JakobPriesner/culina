import type { Pose } from '$shell/olli/poses';

import type { IntakeJob } from './intakes.svelte';

/** The stages an intake walks through while it is being worked on, in order. */
export const stages = ['reading', 'thinking', 'writing', 'ready'];

/** How Olli looks while the intake is at this stage. */
export function poseFor(stage: IntakeJob['stage'] | undefined): Pose {
  switch (stage) {
    case 'ready':
      return 'idea';
    case 'failed':
      return 'puzzled';
    case 'writing':
    case 'saving':
      return 'writing';
    case 'thinking':
      return 'thinking';
    default:
      return 'watching';
  }
}

/**
 * Which of the `stages` is under way: -1 while still queued, and saving counts
 * as writing, which is the last of the work.
 */
export function stepOf(stage: IntakeJob['stage'] | undefined): number {
  if (stage === 'queued') {
    return -1;
  }

  return stage === 'saving' ? 2 : stages.indexOf(stage ?? '');
}
