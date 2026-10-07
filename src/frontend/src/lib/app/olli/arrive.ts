import { cubicOut } from 'svelte/easing';

import { perform } from './performances';
import { idleBlinks, poses, restAfter, type Pose, type PoseSpec } from './poses';
import type { Rig } from './rig.svelte';

/** What arriving in a pose needs from the component that owns the timers. */
interface Arrival {
  rig: Rig;
  later: (ms: number, run: () => void) => unknown;
  /** Cancels every pending beat. */
  rest: () => void;
  blink: () => void;
  /** Whether anybody can see Olli right now. */
  watched: () => boolean;
  working: () => boolean;
}

/** Every part heads for the pose's resting place, at once or by spring. */
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

/** The first arrival fades in, dropping into place unless it is Olli's day job. */
function fadeIn(rig: Rig, next: Pose, target: PoseSpec): void {
  // Fading is not motion, so even a reader who asked for less of it sees
  // Olli arrive rather than pop in.
  void rig.shown.set(0, { duration: 0 });
  void rig.shown.set(1, { duration: next !== 'onDuty' ? 320 : 200, easing: cubicOut });

  if (next !== 'onDuty') {
    void rig.lift.set(next === 'peeking' ? 46 : 8 + target.sag, { instant: true });
    rig.lift.target = target.sag;
  }
}

/** The steam that means something — a question, sleep — still shows when still. */
function stillSteam(steam: PoseSpec['steam']): number {
  if (steam === 'bulb') return 0.5;
  return steam === 'question' || steam === 'sleep' ? 1 : 0;
}

/** Props and paint are left exactly as they would be at the end of a performance. */
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
 * Olli's arrival in a pose, as a function to call each time the pose changes.
 *
 * Remembers whether it has arrived before: the first arrival fades in, later
 * ones blink on the way. When there is work being done it starts over after
 * resting, so the movement carries on for as long as the work does.
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
