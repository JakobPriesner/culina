import { cubicOut } from 'svelte/easing';

import { perform } from './performances';
import { idleBlinks, poses, restAfter, type Pose, type PoseSpec } from './poses';
import type { Rig } from './rig.svelte';

interface Arrival {
  rig: Rig;
  later: (ms: number, run: () => void) => unknown;
  /** Cancels every pending beat. */
  rest: () => void;
  blink: () => void;
  watched: () => boolean;
  working: () => boolean;
}

function settle(rig: Rig, target: PoseSpec, instant: boolean): void {
  void rig.tilt.set(target.tilt, { instant });
  void rig.armL.set(target.arms[0], { instant });
  void rig.armR.set(target.arms[1], { instant });
  void rig.hat.set(target.hatTilt, { instant });
  void rig.lookX.set(target.look[0], { instant });
  void rig.lookY.set(target.look[1], { instant });
  void rig.lift.set(target.sag, { instant });
  void rig.squashX.set(1, { instant });
  void rig.squashY.set(1, { instant });
  void rig.lean.set(0, { instant });
}

function fadeIn(rig: Rig, next: Pose, target: PoseSpec): void {
  // Fading is not motion, so it stays even with reduced motion.
  void rig.shown.set(0, { duration: 0 });
  void rig.shown.set(1, { duration: next !== 'onDuty' ? 320 : 200, easing: cubicOut });

  if (next !== 'onDuty') {
    void rig.lift.set(next === 'peeking' ? 46 : 8 + target.sag, { instant: true });
    rig.lift.target = target.sag;
  }
}

/** Meaningful steam (question, sleep) still shows when not animating. */
function stillSteam(steam: PoseSpec['steam']): number {
  if (steam === 'bulb') return 0.5;
  return steam === 'question' || steam === 'sleep' ? 1 : 0;
}

function holdStill(rig: Rig, next: Pose, target: PoseSpec): void {
  rig.drawingPhase = 'still';
  rig.warmBrush = false;
  void rig.lid.set(1, { duration: 0 });
  void rig.steam.set(stillSteam(target.steam), { duration: 0 });

  void rig.pencil.set(next === 'writing' ? 3 : 0, { duration: 0 });
  void rig.pencilX.set(3, { duration: 0 });
  void rig.pencilY.set(7, { duration: 0 });
  void rig.penLift.set(0, { duration: 0 });
  void rig.brushX.set(108, { duration: 0 });
  void rig.brushY.set(87, { duration: 0 });
  void rig.paint.set(next === 'drawing' ? 3 : 0, { duration: 0 });
}

function steamDuration(steam: PoseSpec['steam']): number {
  return steam === 'bulb' ? 2400 : steam === 'sparks' ? 1100 : 750;
}

/**
 * Arrival in a pose, called on each pose change: the first fades in, later ones blink.
 * While working it restarts after resting, so the movement lasts as long as the work.
 */
export function createArrival({ rig, later, rest, blink, watched, working }: Arrival) {
  let arrived = false;

  function arrive(next: Pose, animate: boolean): void {
    const target = poses[next];
    const first = !arrived;

    arrived = true;
    settle(rig, target, !animate);

    if (!animate) {
      void rig.shown.set(1, { duration: 0 });
    } else if (first) {
      fadeIn(rig, next, target);
    }

    void rig.steam.set(0, { duration: 0 });

    if (!animate) {
      holdStill(rig, next, target);
      return;
    }

    void rig.steam.set(1, { duration: steamDuration(target.steam), delay: 150 });

    if (!first) {
      blink();
    }

    if (target.glance) {
      const [x, y] = target.glance;

      later(200, () => {
        rig.lookX.target = target.look[0] + x;
        rig.lookY.target = target.look[1] + y;
      });
    }

    perform(next, { rig, later, blink, working: working() });
    idleBlinks().forEach((ms) => later(ms, blink));
    later(next === 'drawing' && working() ? 28000 : restAfter, () => {
      rest();
      if (working() && watched()) later(900, () => arrive(next, true));
    });
  }

  return arrive;
}
