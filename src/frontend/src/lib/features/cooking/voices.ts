/** Which of the device's voices should read a step in a language; speech engines list them in no useful order. */

/** Names that mark a better voice: Apple's downloaded ones, Microsoft's and Google's neural and network voices. */
const better = /premium|enhanced|neural|natural|online|google/i;

/** Names that mark a worse one: Apple's compact voices, eSpeak, and the old robotic or novelty ones. */
const worse =
  /compact|espeak|\b(albert|bad news|bahh|bells|boing|bubbles|cellos|good news|jester|organ|superstar|trinoids|whisper|wobble|zarvox|fred|junior|kathy|ralph)\b/i;

/** The language part of a tag: `de-AT` and `de_DE` are `de`. */
const languageOf = (tag: string): string => tag.toLowerCase().split(/[-_]/)[0] ?? '';

function score(voice: SpeechSynthesisVoice): number {
  return (
    (better.test(voice.name) ? 4 : 0) +
    (worse.test(voice.name) ? -4 : 0) +
    // Works offline and has no cap on how long it may speak, which suits a kitchen with weak wifi.
    (voice.localService ? 1 : 0) +
    (voice.default ? 0.5 : 0)
  );
}

/** The best voice for the language, or `undefined` when the device has none, so the engine falls back on `lang`. */
export function pickVoice(
  voices: readonly SpeechSynthesisVoice[],
  language: string
): SpeechSynthesisVoice | undefined {
  const wanted = languageOf(language);
  const candidates = voices.filter((voice) => languageOf(voice.lang) === wanted);

  // Ties go to the earlier voice, which is the order the engine lists them in.
  return candidates.reduce<SpeechSynthesisVoice | undefined>(
    (best, voice) => (!best || score(voice) > score(best) ? voice : best),
    undefined
  );
}
