/**
 * Tactile micro-haptics for mobile devices.
 *
 * Tapping a checkbox in the supermarket or advancing a cooking step with a wet
 * thumb benefits from subtle physical confirmation. Degrades silently on
 * devices or browsers without vibration support.
 */
export const haptics = {
  /** A subtle tick when checking off an ingredient or toggling a control. */
  tick(): void {
    if (typeof navigator !== 'undefined' && 'vibrate' in navigator) {
      try {
        navigator.vibrate(10);
      } catch {
        // Ignored.
      }
    }
  },

  /** A crisp tap when advancing or rewinding a cooking step. */
  step(): void {
    if (typeof navigator !== 'undefined' && 'vibrate' in navigator) {
      try {
        navigator.vibrate(25);
      } catch {
        // Ignored.
      }
    }
  },

  /** Celebratory pattern when completing a cooking session or clearing a shopping list. */
  celebrate(): void {
    if (typeof navigator !== 'undefined' && 'vibrate' in navigator) {
      try {
        navigator.vibrate([15, 60, 30]);
      } catch {
        // Ignored.
      }
    }
  },

  /** An alert pulse pattern when a kitchen timer has finished. */
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
