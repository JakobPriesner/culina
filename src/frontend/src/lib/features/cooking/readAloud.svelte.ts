import type { Scaling } from '$features/recipes/surface/scaled.svelte';
import type { Step } from '$features/recipes/types';
import { m, type Locale } from '$shell/i18n';

import { stepSpeech } from './stepSpeech';
import { pickVoice } from './voices';

const key = 'culina.cook.readAloud';

/** How long the voice stays quiet after an alarm starts: the chime is about a second long and must be heard. */
const alarmHoldMs = 1500;

/** Engines drop long utterances (Chrome's online voices stop near fifteen seconds), so a step is said sentence by sentence. */
const chunkLength = 200;

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

/** The device's speech engine; none without one, or without utterances to give it (old WebViews). */
const engine = (): SpeechSynthesis | null =>
  typeof window === 'undefined' || typeof SpeechSynthesisUtterance === 'undefined'
    ? null
    : (window.speechSynthesis ?? null);

/** Sentences packed into chunks no longer than `chunkLength`, unless one sentence alone is longer. */
export function chunksOf(text: string): string[] {
  const chunks: string[] = [];

  for (const sentence of text.match(/[^.!?…]+[.!?…]*\s*/g) ?? [text]) {
    const last = chunks.length - 1;

    if (last >= 0 && chunks[last]!.length + sentence.length <= chunkLength) {
      chunks[last] += sentence;
    } else {
      chunks.push(sentence);
    }
  }

  return chunks.map((chunk) => chunk.trim()).filter(Boolean);
}

/**
 * Steps read aloud while cooking, by the device's own speech synthesis (nothing leaves the device), in the recipe's
 * language and the best voice the device has for it. One reading at a time: a new step, the step's own speaker or
 * stopping replaces it, and a timer's alarm outranks it. Whether each step is read as it comes up (`on`) is kept on
 * the device, like the kitchen's lighting.
 */
class ReadAloud {
  on = $state(recall());

  /** Something is being said now, from the toggle or a step's own speaker. */
  speaking = $state(false);

  /** Which reading is current; the end events of a cancelled one arrive late and must not end the next. */
  #reading = 0;
  #heldUntil = 0;

  get supported(): boolean {
    return engine() !== null;
  }

  toggle(): void {
    this.on = !this.on;
    remember(this.on);

    if (!this.on) this.stop();
  }

  /** Says the text, unless there is nothing to say, no engine, or an alarm is ringing; whether it did. */
  speak(text: string, language: string): boolean {
    const synth = engine();

    if (!synth || !text.trim() || Date.now() < this.#heldUntil) return false;

    // A new step interrupts the last one: what is read is always the step on screen.
    this.stop();

    const mine = this.#reading;
    const voice = pickVoice(synth.getVoices(), language);
    const chunks = chunksOf(text);

    this.speaking = true;

    chunks.forEach((chunk, index) => {
      const utterance = new SpeechSynthesisUtterance(chunk);

      utterance.lang = voice?.lang ?? (language === 'de' ? 'de-DE' : 'en-GB');
      if (voice) utterance.voice = voice;

      const finished = () => {
        if (this.#reading === mine && index === chunks.length - 1) this.speaking = false;
      };

      utterance.onend = finished;
      utterance.onerror = finished;
      synth.speak(utterance);
    });

    return true;
  }

  /** A step's own speaker: says it, or stops what is being said. */
  readStep(text: string, language: string): void {
    if (this.speaking) {
      this.stop();
    } else {
      this.speak(text, language);
    }
  }

  stop(): void {
    this.#reading += 1;
    this.speaking = false;
    engine()?.cancel();
  }

  /** An alarm outranks the voice: stops it and keeps it quiet for the length of the chime. */
  interrupt(): void {
    this.stop();
    this.#heldUntil = Date.now() + alarmHoldMs;
  }
}

export const readAloud = new ReadAloud();

/**
 * A step as it is said: its name, then its words at the amounts on screen, cleaned up for a voice (fractions, units
 * and markdown said as words, each paragraph and list item a sentence of its own).
 */
export function spokenStep(step: Step, number: number, scaling: Scaling, language: string): string {
  const words = stepSpeech(step, scaling.amountFor, scaling.nameFor, language as Locale);

  return `${step.title ?? m['recipe.step']({ number })}. ${words}`;
}
