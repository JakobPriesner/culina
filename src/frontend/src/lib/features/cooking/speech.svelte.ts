import { readDevice, writeDevice } from '$shell/deviceStorage';
import type { Locale } from '$shell/i18n';

import { pickVoice } from './voices';

const autoReadKey = 'culina.speech.autoRead';

/** How long speech stays off after an alarm starts: the chime is about a second long and must be heard. */
const alarmHoldMs = 1500;

/** Engines drop long utterances (Chrome's online voices stop near fifteen seconds), so a step is spoken sentence by sentence. */
const chunkLength = 200;

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
 * Reads text aloud through the browser's speech engine, one reading at a time: a new one replaces the last.
 * Absent engines are not an error; `supported` says so and the controls stay away.
 */
export function createSpeech(
  engine: () => SpeechSynthesis | undefined = () => globalThis.speechSynthesis
) {
  let speaking = $state(false);
  let autoRead = $state(false);

  /** Which reading is current; the end and error events of a cancelled one arrive late and must not touch the next. */
  let reading = 0;
  let heldUntil = 0;

  const synth = () => (typeof SpeechSynthesisUtterance === 'undefined' ? undefined : engine());

  function cancel() {
    reading += 1;
    speaking = false;
    synth()?.cancel();
  }

  function speak(text: string, locale: Locale): boolean {
    const synthesis = synth();

    if (!synthesis || !text.trim() || Date.now() < heldUntil) {
      return false;
    }

    cancel();

    const mine = reading;
    const voice = pickVoice(synthesis.getVoices(), locale);
    const chunks = chunksOf(text);

    speaking = true;

    chunks.forEach((chunk, index) => {
      const utterance = new SpeechSynthesisUtterance(chunk);

      utterance.lang = voice?.lang ?? locale;

      if (voice) {
        utterance.voice = voice;
      }

      const finished = () => {
        if (reading === mine && index === chunks.length - 1) {
          speaking = false;
        }
      };

      utterance.onend = finished;
      utterance.onerror = finished;
      synthesis.speak(utterance);
    });

    return true;
  }

  return {
    get supported() {
      return synth() !== undefined;
    },

    get speaking() {
      return speaking;
    },

    get autoRead() {
      return autoRead;
    },

    speak,
    cancel,

    /** The speaker button: reads, or stops what is being read. */
    toggle(text: string, locale: Locale) {
      if (speaking) {
        cancel();
      } else {
        speak(text, locale);
      }
    },

    /** An alarm outranks the voice: stops it and keeps it quiet for the length of the chime. */
    interrupt() {
      cancel();
      heldUntil = Date.now() + alarmHoldMs;
    },

    load() {
      autoRead = readDevice(autoReadKey) === 'true';
    },

    setAutoRead(value: boolean) {
      autoRead = value;
      writeDevice(autoReadKey, String(value));
    }
  };
}

export const speech = createSpeech();
