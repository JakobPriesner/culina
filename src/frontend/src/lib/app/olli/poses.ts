/** Olli's poses as data: where the parts sit plus one prop and a one-off arrival action, so one component can spring between any two. */
export type Pose =
  | 'hello'
  | 'peeking'
  | 'reading'
  | 'watching'
  | 'thinking'
  | 'writing'
  | 'drawing'
  | 'idea'
  | 'celebrating'
  | 'puzzled'
  | 'dozing'
  | 'unplugged'
  | 'onDuty';

export type Eyes = 'open' | 'happy' | 'closed';
export type Mouth = 'smile' | 'open' | 'o' | 'flat' | 'wobble';

export interface PoseSpec {
  readonly eyes: Eyes;
  readonly mouth: Mouth;
  readonly brows?: 'puzzled' | 'worried';
  /** Handle angles in degrees; positive raises like an arm, past about 60 it hides behind the rim. */
  readonly arms: readonly [left: number, right: number];
  readonly tilt: number;
  readonly hatTilt: number;
  readonly look: readonly [x: number, y: number];
  /** Where the eyes glance once, after arriving: towards the page's action. */
  readonly glance?: readonly [x: number, y: number];
  readonly sag: number;
  readonly prop?: 'card' | 'plug' | 'ticket' | 'phone' | 'pencil' | 'brush';
  /** What rises from the pot on arrival; nothing when unplugged. */
  readonly steam: 'wisp' | 'question' | 'sleep' | 'sparks' | 'none' | 'bulb';
  /** Stuck or refused: no playfulness (Mailchimp's rule), so Olli ignores being poked. */
  readonly sombre: boolean;
}

export const poses: Record<Pose, PoseSpec> = {
  hello: {
    eyes: 'open',
    mouth: 'open',
    arms: [55, 0],
    tilt: 0,
    hatTilt: 0,
    look: [0, 0],
    glance: [1.5, 1.5],
    sag: 0,
    steam: 'wisp',
    sombre: false
  },
  peeking: {
    eyes: 'open',
    mouth: 'o',
    arms: [0, 0],
    tilt: 3,
    hatTilt: 0,
    look: [0, 0],
    glance: [1.5, 2],
    sag: 0,
    steam: 'wisp',
    sombre: false
  },
  reading: {
    eyes: 'open',
    mouth: 'smile',
    arms: [-10, -10],
    tilt: 0,
    hatTilt: 0,
    look: [0, 2.4],
    sag: 0,
    prop: 'card',
    steam: 'wisp',
    sombre: false
  },
  watching: {
    eyes: 'open',
    mouth: 'smile',
    arms: [-8, 32],
    tilt: 2,
    hatTilt: 1,
    look: [3, 1.5],
    sag: 0,
    prop: 'phone',
    steam: 'none',
    sombre: false
  },
  thinking: {
    eyes: 'open',
    mouth: 'o',
    arms: [0, 12],
    tilt: -4,
    hatTilt: -2,
    look: [-2, -3],
    sag: 0,
    steam: 'none',
    sombre: false
  },
  writing: {
    eyes: 'open',
    mouth: 'smile',
    arms: [-10, -18],
    tilt: 3,
    hatTilt: 1,
    look: [1, 3],
    sag: 0,
    prop: 'pencil',
    steam: 'none',
    sombre: false
  },
  drawing: {
    eyes: 'open',
    mouth: 'smile',
    arms: [-10, -18],
    tilt: -2,
    hatTilt: 2,
    look: [3, 2],
    sag: 0,
    prop: 'brush',
    steam: 'none',
    sombre: false
  },
  idea: {
    eyes: 'happy',
    mouth: 'smile',
    arms: [18, 18],
    tilt: 0,
    hatTilt: -2,
    look: [0, -1],
    sag: 0,
    steam: 'bulb',
    sombre: false
  },
  celebrating: {
    eyes: 'happy',
    mouth: 'open',
    arms: [55, 55],
    tilt: 0,
    hatTilt: 0,
    look: [0, 0],
    sag: 0,
    steam: 'sparks',
    sombre: false
  },
  puzzled: {
    eyes: 'open',
    mouth: 'wobble',
    brows: 'puzzled',
    arms: [0, 0],
    tilt: -7,
    hatTilt: -6,
    look: [-1.5, -1],
    glance: [1.5, 1.5],
    sag: 0,
    steam: 'question',
    sombre: false
  },
  dozing: {
    eyes: 'closed',
    mouth: 'smile',
    arms: [-15, -15],
    tilt: 4,
    hatTilt: 10,
    look: [0, 0],
    sag: 0,
    steam: 'sleep',
    sombre: true
  },
  unplugged: {
    eyes: 'open',
    mouth: 'wobble',
    brows: 'worried',
    arms: [-20, -20],
    tilt: 0,
    hatTilt: 0,
    look: [1.5, 1],
    sag: 2,
    prop: 'plug',
    steam: 'none',
    sombre: true
  },
  onDuty: {
    eyes: 'open',
    mouth: 'flat',
    arms: [0, 0],
    tilt: 0,
    hatTilt: 0,
    look: [0, 0],
    sag: 0,
    prop: 'ticket',
    steam: 'none',
    sombre: true
  }
};

/** When the idle blinks fall: 1.5-4 s after arriving, sometimes a second before 5 s, none after (WCAG 2.2.2); random so it does not read as a machine. */
export function idleBlinks(random: () => number = Math.random): number[] {
  const first = 1500 + random() * 2500;

  return random() < 0.6 ? [first, Math.max(first + 600, 4000 + random() * 800)] : [first];
}

export const restAfter = 5000;
