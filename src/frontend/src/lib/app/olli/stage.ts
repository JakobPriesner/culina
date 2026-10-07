import type { Rig } from './rig.svelte';

/** What a performance may use: the parts to move and a way to schedule moving them. */
export interface Stage {
  rig: Rig;
  later: (ms: number, run: () => void) => unknown;
  blink: () => void;
  /** Whether something is still being processed, which makes drawing go on longer. */
  working: boolean;
}

export type Performance = (stage: Stage) => void;
