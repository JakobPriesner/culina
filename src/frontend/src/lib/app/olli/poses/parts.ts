import type { Component } from 'svelte';

import type { Pose } from '../poses';
import type { Rig } from '../rig.svelte';
import OlliPoseDrawing from './OlliPoseDrawing.svelte';
import OlliPoseOnDuty from './OlliPoseOnDuty.svelte';
import OlliPoseReading from './OlliPoseReading.svelte';
import OlliPoseUnplugged from './OlliPoseUnplugged.svelte';
import OlliPoseWatching from './OlliPoseWatching.svelte';
import OlliPoseWriting from './OlliPoseWriting.svelte';

export type PosePart = Component<{ motion: boolean; rig: Rig }>;

/** Beside the pot, drawn before it, so the pot stands in front. */
export const trailingParts: Partial<Record<Pose, PosePart>> = {
  unplugged: OlliPoseUnplugged,
  onDuty: OlliPoseOnDuty
};

/** In the pot's own frame, drawn last, so they lean and squash with it. */
export const heldParts: Partial<Record<Pose, PosePart>> = {
  watching: OlliPoseWatching,
  reading: OlliPoseReading,
  writing: OlliPoseWriting,
  drawing: OlliPoseDrawing
};
