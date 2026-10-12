/** The four feedback moments; the app maps each to the platform's own haptic (lib/app/nativeShell.ts). */
export type HapticMoment = 'tick' | 'step' | 'celebrate' | 'alarm';

const vibrations: Record<HapticMoment, number | number[]> = {
  tick: 10,
  step: 25,
  celebrate: [15, 60, 30],
  alarm: [250, 100, 250, 100, 500]
};

let nativePlay: ((moment: HapticMoment) => void) | null = null;

/** Registered by the installed app: iOS has no `navigator.vibrate` at all, so its haptics come from the Taptic Engine. */
export function useNativeHaptics(play: (moment: HapticMoment) => void): void {
  nativePlay = play;
}

function feel(moment: HapticMoment): void {
  if (nativePlay) {
    nativePlay(moment);

    return;
  }

  if (typeof navigator !== 'undefined' && 'vibrate' in navigator) {
    try {
      navigator.vibrate(vibrations[moment]);
    } catch {
      // Ignored.
    }
  }
}

/** Subtle haptics for mobile; silent where vibration isn't supported. */
export const haptics = {
  tick: () => feel('tick'),
  step: () => feel('step'),
  celebrate: () => feel('celebrate'),
  alarm: () => feel('alarm')
};
