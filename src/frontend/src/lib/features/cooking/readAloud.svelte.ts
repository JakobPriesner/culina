import type { Scaling } from '$features/recipes/surface/scaled.svelte';
import type { Step } from '$features/recipes/types';
import { m } from '$shell/i18n';

const key = 'culina.cook.readAloud';

function recall(): boolean {
  try {
    return localStorage.getItem(key) === 'on';
  } catch {
    return false;
  }
}

function remember(on: boolean): void {
  try {
    localStorage.setItem(key, on ? 'on' : 'off');
  } catch {
    // On for this visit only.
  }
}

const speech = (): SpeechSynthesis | null =>
  typeof window !== 'undefined' &&
  'speechSynthesis' in window &&
  'SpeechSynthesisUtterance' in window
    ? window.speechSynthesis
    : null;

/**
 * Steps read aloud while cooking, by the device's own speech synthesis (nothing leaves the device), in the
 * recipe's language. The choice is kept on the device, like the kitchen's lighting.
 */
class ReadAloud {
  on = $state(recall());

  get supported(): boolean {
    return speech() !== null;
  }

  toggle(): void {
    this.on = !this.on;
    remember(this.on);

    if (!this.on) this.stop();
  }

  speak(text: string, language: string): void {
    const synth = speech();

    if (!synth) return;

    // A new step interrupts the last one: what is read is always the step on screen.
    synth.cancel();

    const utterance = new SpeechSynthesisUtterance(text);
    utterance.lang = language === 'de' ? 'de-DE' : 'en-GB';
    utterance.voice = synth.getVoices().find((voice) => voice.lang.startsWith(language)) ?? null;
    synth.speak(utterance);
  }

  stop(): void {
    speech()?.cancel();
  }
}

export const readAloud = new ReadAloud();

/** Markdown marks a voice would spell out ("Stern Stern"), and the no-break spaces of amounts. */
const unmarked = (text: string) =>
  text
    .replace(/[*_`#>]+/g, '')
    .replace(/^\s*(?:[-+]|\d+[.)])\s+/gm, '')
    .replace(/\u00a0/g, ' ');

/** A step as it is said: its name, then its words with every ingredient at the amount on screen. */
export function spokenStep(step: Step, number: number, scaling: Scaling): string {
  const words = step.segments
    .map((segment) =>
      segment.kind === 'text'
        ? segment.text
        : `${scaling.amountFor(segment).text} ${scaling.nameFor(segment)}`.trim()
    )
    .join('');

  return `${step.title ?? m['recipe.step']({ number })}. ${unmarked(words)}`;
}
