import type { Pose } from '$shell/olli/poses';

import type { IntakeJob } from './intakes.svelte';

export const stages = ['reading', 'thinking', 'writing', 'ready'];

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

/** Index into `stages`: -1 while queued; saving counts as writing. */
export function stepOf(stage: IntakeJob['stage'] | undefined): number {
  if (stage === 'queued') {
    return -1;
  }

  return stage === 'saving' ? 2 : stages.indexOf(stage ?? '');
}
