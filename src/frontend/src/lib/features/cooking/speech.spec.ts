import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { chunksOf, readAloud } from './readAloud.svelte';

class FakeUtterance {
  lang = '';
  voice: SpeechSynthesisVoice | null = null;
  onend: (() => void) | null = null;
  onerror: (() => void) | null = null;

  constructor(readonly text: string) {}
}

/** The browser's engine, as the one read-aloud store finds it on the window. */
function fakeEngine(voices: Partial<SpeechSynthesisVoice>[] = []) {
  const queue: FakeUtterance[] = [];
  const engine = {
    speak: vi.fn((utterance: FakeUtterance) => void queue.push(utterance)),
    // Like the browser, a cancelled queue reports an error for what was in it.
    cancel: vi.fn(() => {
      for (const utterance of queue.splice(0)) utterance.onerror?.();
    }),
    getVoices: () => voices as SpeechSynthesisVoice[]
  } as unknown as SpeechSynthesis;

  vi.stubGlobal('speechSynthesis', engine);

  return { queue, engine };
}

beforeEach(() => {
  vi.stubGlobal('SpeechSynthesisUtterance', FakeUtterance);
});

afterEach(() => {
  readAloud.stop();
  vi.useRealTimers();
  vi.unstubAllGlobals();
});

describe('chunksOf', () => {
  it('packs sentences into chunks the engine will not drop', () => {
    const sentence = `${'word '.repeat(18).trim()}.`;
    const chunks = chunksOf(`${sentence} ${sentence} ${sentence}`);

    expect(chunks).toHaveLength(2);
    expect(chunks.join(' ')).toBe(`${sentence} ${sentence} ${sentence}`);
  });

  it('keeps short text whole, and speaks a sentence without an end', () => {
    expect(chunksOf('Stir. Serve.')).toEqual(['Stir. Serve.']);
    expect(chunksOf('Stir')).toEqual(['Stir']);
    expect(chunksOf('')).toEqual([]);
  });
});

describe('reading aloud', () => {
  it('reads with the best voice for the language', () => {
    const { queue } = fakeEngine([
      { name: 'Anna', lang: 'de-DE', localService: true },
      { name: 'Anna (Premium)', lang: 'de-DE', localService: true }
    ]);

    expect(readAloud.speak('Rühren.', 'de')).toBe(true);
    expect(readAloud.speaking).toBe(true);
    expect(queue[0]?.voice?.name).toBe('Anna (Premium)');
    expect(queue[0]?.lang).toBe('de-DE');
  });

  it('falls back on the language when the device has no voice yet', () => {
    const { queue } = fakeEngine();

    readAloud.speak('Stir.', 'en');

    expect(queue[0]?.voice).toBeNull();
    expect(queue[0]?.lang).toBe('en-GB');
  });

  it('stops speaking when the last chunk ends', () => {
    const { queue } = fakeEngine();

    readAloud.speak('Stir the sauce well. '.repeat(30), 'en');

    expect(queue.length).toBeGreaterThan(1);

    queue[0]!.onend?.();
    expect(readAloud.speaking).toBe(true);

    queue.at(-1)!.onend?.();
    expect(readAloud.speaking).toBe(false);
  });

  it('stops cleanly, and the late error of the stopped reading does not touch the next', () => {
    const { queue } = fakeEngine();

    readAloud.speak('One.', 'en');
    const first = queue[0]!;

    readAloud.stop();
    expect(readAloud.speaking).toBe(false);

    readAloud.speak('Two.', 'en');
    first.onerror?.();

    expect(readAloud.speaking).toBe(true);
  });

  it('replaces a reading with the next', () => {
    const { engine } = fakeEngine();

    readAloud.speak('One.', 'en');
    readAloud.speak('Two.', 'en');

    expect(engine.cancel).toHaveBeenCalledTimes(2);
    expect(readAloud.speaking).toBe(true);
  });

  it("a step's own speaker reads, then stops", () => {
    fakeEngine();

    readAloud.readStep('Stir.', 'en');
    expect(readAloud.speaking).toBe(true);

    readAloud.readStep('Stir.', 'en');
    expect(readAloud.speaking).toBe(false);
  });

  it('says nothing for empty text', () => {
    const { engine } = fakeEngine();

    expect(readAloud.speak('  ', 'en')).toBe(false);
    expect(engine.speak).not.toHaveBeenCalled();
  });

  it('gives way to an alarm, and stays quiet while it rings', () => {
    vi.useFakeTimers();
    fakeEngine();

    readAloud.speak('Stir.', 'en');
    readAloud.interrupt();

    expect(readAloud.speaking).toBe(false);
    expect(readAloud.speak('Serve.', 'en')).toBe(false);

    vi.advanceTimersByTime(1600);

    expect(readAloud.speak('Serve.', 'en')).toBe(true);
  });

  it('is absent without a speech engine', () => {
    vi.stubGlobal('speechSynthesis', undefined);

    expect(readAloud.supported).toBe(false);
    expect(readAloud.speak('Stir.', 'en')).toBe(false);
    expect(() => readAloud.stop()).not.toThrow();
    expect(() => readAloud.interrupt()).not.toThrow();
  });

  it('is absent without utterances, as in old WebViews', () => {
    fakeEngine();
    vi.stubGlobal('SpeechSynthesisUtterance', undefined);

    expect(readAloud.supported).toBe(false);
  });
});
