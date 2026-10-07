import type { Pose } from './poses';
import { drawing } from './poses/drawing';
import { writing } from './poses/writing';
import type { Performance, Stage } from './stage';

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
