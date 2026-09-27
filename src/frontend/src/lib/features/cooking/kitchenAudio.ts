/**
 * Synthesizes a warm, pleasant kitchen timer bell using the Web Audio API.
 *
 * Avoids extra network payloads and large audio asset bundles.
 * Pre-unlocks during user gestures (timer start) to adhere to browser autoplay policies.
 */
let audioContext: AudioContext | null = null;

function getContext(): AudioContext | null {
  if (typeof window === 'undefined') {
    return null;
  }
  const AudioCtx =
    window.AudioContext ||
    (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
  if (!AudioCtx) {
    return null;
  }
  if (!audioContext || audioContext.state === 'closed') {
    try {
      audioContext = new AudioCtx();
    } catch {
      return null;
    }
  }
  return audioContext;
}

/** Pre-unlocks audio context during a user gesture (e.g. starting a timer). */
export function unlockAudio(): void {
  try {
    const ctx = getContext();
    if (ctx && ctx.state === 'suspended') {
      void ctx.resume();
    }
  } catch {
    // Ignored.
  }
}

/** Plays a dual-tone kitchen chime (D5 587Hz & A5 880Hz). */
export function playKitchenChime(): void {
  try {
    const ctx = getContext();
    if (!ctx) {
      return;
    }
    if (ctx.state === 'suspended') {
      void ctx.resume();
    }

    const now = ctx.currentTime;
    const tones = [587.33, 880];

    for (const freq of tones) {
      const osc = ctx.createOscillator();
      const gain = ctx.createGain();

      osc.type = 'sine';
      osc.frequency.setValueAtTime(freq, now);

      gain.gain.setValueAtTime(0.25, now);
      gain.gain.exponentialRampToValueAtTime(0.0001, now + 1.2);

      osc.connect(gain);
      gain.connect(ctx.destination);

      osc.start(now);
      osc.stop(now + 1.2);
    }
  } catch {
    // Audio unavailable or blocked.
  }
}
