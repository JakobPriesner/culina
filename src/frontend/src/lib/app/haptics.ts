/** Subtle haptics for mobile; silent where vibration isn't supported. */
export const haptics = {
  tick(): void {
    if (typeof navigator !== 'undefined' && 'vibrate' in navigator) {
      try {
        navigator.vibrate(10);
      } catch {
        // Ignored.
      }
    }
  },

  step(): void {
    if (typeof navigator !== 'undefined' && 'vibrate' in navigator) {
      try {
        navigator.vibrate(25);
      } catch {
        // Ignored.
      }
    }
  },

  celebrate(): void {
    if (typeof navigator !== 'undefined' && 'vibrate' in navigator) {
      try {
        navigator.vibrate([15, 60, 30]);
      } catch {
        // Ignored.
      }
    }
  },

  alarm(): void {
    if (typeof navigator !== 'undefined' && 'vibrate' in navigator) {
      try {
        navigator.vibrate([250, 100, 250, 100, 500]);
      } catch {
        // Ignored.
      }
    }
  }
};
