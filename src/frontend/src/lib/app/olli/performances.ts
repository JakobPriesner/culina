import { cubicOut, linear } from 'svelte/easing';

import type { Pose } from './poses';
import type { Rig } from './rig.svelte';

/** What a performance may use: the parts to move and a way to schedule moving them. */
export interface Stage {
  rig: Rig;
  later: (ms: number, run: () => void) => unknown;
  blink: () => void;
  /** Whether something is still being processed, which makes drawing go on longer. */
  working: boolean;
}

type Performance = (stage: Stage) => void;

function hello({ rig, later }: Stage) {
  [35, 62, 38, 55].forEach((angle, i) => later(320 + i * 175, () => (rig.armL.target = angle)));
}

function watching({ rig, later, blink }: Stage) {
  later(700, () => (rig.hat.target = 3));
  later(1300, () => (rig.hat.target = 0));
  later(2100, blink);
}

function thinking({ rig, later }: Stage) {
  later(650, () => {
    rig.lookX.target = 2;
    rig.lookY.target = -3;
  });
  later(1800, () => {
    rig.tilt.target = 3;
    rig.hat.target = 1;
  });
  later(2900, () => {
    rig.lookX.target = -2;
    rig.tilt.target = -4;
    rig.hat.target = -2;
  });
}

function writing({ rig, later }: Stage) {
  void rig.pencil.set(0, { duration: 0 });
  [0, 1, 2].forEach((line) => {
    const start = line * 1150;
    later(start, () => void rig.penLift.set(3, { duration: 80 }));
    later(start + 90, () => {
      void rig.pencilX.set(-15, { duration: 180, easing: cubicOut });
      void rig.pencilY.set(line * 6 - 5, { duration: 180, easing: cubicOut });
    });
    later(start + 280, () => void rig.penLift.set(0, { duration: 100 }));
    later(start + 400, () => {
      void rig.pencil.set(line + 1, { duration: 650, easing: linear });
      void rig.pencilX.set(line === 2 ? 3 : 14, { duration: 650, easing: linear });
    });
  });
  later(3400, () => void rig.penLift.set(2, { duration: 150 }));
  later(3650, () => (rig.lookY.target = 1));
}

/**
 * A complete work phrase, with brisk strokes and purposeful holds. The
 * painting is kept on later cycles: Olli refines it, never wipes it.
 */
function drawing({ rig, later, blink, working }: Stage) {
  const stroke = (at: number, x: number, y: number, progress: number) =>
    later(at, () => {
      rig.drawingPhase = 'painting';
      void rig.brushX.set(x, { duration: 550, easing: cubicOut });
      void rig.brushY.set(y, { duration: 550, easing: cubicOut });
      void rig.paint.set(Math.max(rig.paint.current, progress), { duration: 550, easing: linear });
      rig.lookX.target = 3;
      rig.lookY.target = 2;
    });
  const inspect = (at: number) =>
    later(at, () => {
      rig.drawingPhase = 'inspecting';
      void rig.brushX.set(111, { duration: 300, easing: cubicOut });
      void rig.brushY.set(70, { duration: 300, easing: cubicOut });
      rig.tilt.target = -4;
      rig.lookY.target = 0;
    });
  const colour = (at: number, warm: boolean) =>
    later(at, () => {
      rig.drawingPhase = 'choosing-colour';
      rig.warmBrush = warm;
      void rig.brushX.set(80, { duration: 400, easing: cubicOut });
      void rig.brushY.set(94, { duration: 400, easing: cubicOut });
      rig.lookX.target = -3;
      rig.lookY.target = 3;
      rig.tilt.target = -2;
    });

  colour(0, false);
  stroke(1200, 106, 75, 0.4);
  stroke(2100, 98, 83, 0.7);
  stroke(3000, 108, 87, 1);
  inspect(4100);

  if (!working) {
    stroke(3800, 108, 87, 3);
    return;
  }

  colour(6500, true);
  stroke(8500, 102, 78, 1.3);
  stroke(9600, 107, 84, 1.7);
  stroke(10800, 98, 82, 2);
  inspect(12200);
  colour(15000, false);
  stroke(17200, 108, 80, 2.3);
  stroke(18300, 100, 85, 2.6);
  stroke(19400, 105, 77, 3);
  inspect(20700);
  stroke(22800, 108, 83, 3);
  inspect(24000);
  later(25700, blink);
}

function idea({ rig, later }: Stage) {
  later(120, () => {
    rig.lift.target = -3;
    rig.hat.target = -4;
  });
  later(650, () => {
    rig.lift.target = 0;
    rig.hat.target = 0;
  });
}

// Three lines read, then a hold: a loop past five seconds is motion somebody
// would have to be able to stop.
function reading({ rig, later }: Stage) {
  for (let i = 0; i < 6; i++) {
    later(i * 600, () => (rig.lookX.target = i % 2 ? 1.5 : -1.5));
  }
  later(3600, () => (rig.lookX.target = 0));
}

function celebrating({ rig, later }: Stage) {
  rig.squash(1.04, 0.94);
  later(120, () => {
    rig.lift.target = -10;
    rig.squash(0.96, 1.06);
    rig.hat.target = -6;
  });
  later(320, () => {
    rig.lift.target = 0;
    rig.hat.target = 5;
  });
  later(440, () => rig.squash(1.05, 0.95));
  later(560, () => {
    rig.squash(1, 1);
    rig.hat.target = 0;
  });
}

function dozing({ rig, later }: Stage) {
  rig.squash(1.015, 1.015);
  later(1200, () => rig.squash(1, 1));
}

/** What each pose does once, on arriving in it. The rest simply hold their pose. */
const performances: Partial<Record<Pose, Performance>> = {
  hello,
  watching,
  thinking,
  writing,
  drawing,
  idea,
  reading,
  celebrating,
  dozing
};

export function perform(pose: Pose, stage: Stage): void {
  performances[pose]?.(stage);
}
