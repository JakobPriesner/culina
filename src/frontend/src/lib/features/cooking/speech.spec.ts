import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { chunksOf, createSpeech } from './speech.svelte';

class FakeUtterance {
  lang = '';
  voice: SpeechSynthesisVoice | null = null;
  onend: (() => void) | null = null;
  onerror: (() => void) | null = null;

  constructor(readonly text: string) {}
}

function fakeEngine(voices: Partial<SpeechSynthesisVoice>[] = []) {
  const queue: FakeUtterance[] = [];

  return {
    queue,
    engine: {
      speak: vi.fn((utterance: FakeUtterance) => void queue.push(utterance)),
      // Like the browser, a cancelled queue reports an error for what was in it.
      cancel: vi.fn(() => {
        for (const utterance of queue.splice(0)) utterance.onerror?.();
      }),
      getVoices: () => voices as SpeechSynthesisVoice[]
    } as unknown as SpeechSynthesis
  };
}

beforeEach(() => {
  vi.stubGlobal('SpeechSynthesisUtterance', FakeUtterance);
  localStorage.clear();
});

afterEach(() => {
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

describe('speech', () => {
  it('reads with the best voice for the language', () => {
    const { engine, queue } = fakeEngine([
      { name: 'Anna', lang: 'de-DE', localService: true },
      { name: 'Anna (Premium)', lang: 'de-DE', localService: true }
    ]);
    const speech = createSpeech(() => engine);

    expect(speech.speak('Rühren.', 'de')).toBe(true);
    expect(speech.speaking).toBe(true);
    expect(queue[0]?.voice?.name).toBe('Anna (Premium)');
    expect(queue[0]?.lang).toBe('de-DE');
  });

  it('falls back on the language when the device has no voice yet', () => {
    const { engine, queue } = fakeEngine();
    const speech = createSpeech(() => engine);

    speech.speak('Stir.', 'en');

    expect(queue[0]?.voice).toBeNull();
    expect(queue[0]?.lang).toBe('en');
  });

  it('stops speaking when the last chunk ends', () => {
    const { engine, queue } = fakeEngine();
    const speech = createSpeech(() => engine);

    speech.speak('Stir the sauce well. '.repeat(30), 'en');

    expect(queue.length).toBeGreaterThan(1);

    queue[0]!.onend?.();
    expect(speech.speaking).toBe(true);

    queue.at(-1)!.onend?.();
    expect(speech.speaking).toBe(false);
  });

  it('cancels cleanly, and the late error of the cancelled reading does not touch the next', () => {
    const { engine, queue } = fakeEngine();
    const speech = createSpeech(() => engine);

    speech.speak('One.', 'en');
    const first = queue[0]!;

    speech.cancel();
    expect(speech.speaking).toBe(false);

    speech.speak('Two.', 'en');
    first.onerror?.();

    expect(speech.speaking).toBe(true);
  });

  it('replaces a reading with the next', () => {
    const { engine } = fakeEngine();
    const speech = createSpeech(() => engine);

    speech.speak('One.', 'en');
    speech.speak('Two.', 'en');

    expect(engine.cancel).toHaveBeenCalledTimes(2);
    expect(speech.speaking).toBe(true);
  });

  it('toggles between reading and stopping', () => {
    const { engine } = fakeEngine();
    const speech = createSpeech(() => engine);

    speech.toggle('Stir.', 'en');
    expect(speech.speaking).toBe(true);

    speech.toggle('Stir.', 'en');
    expect(speech.speaking).toBe(false);
  });

  it('says nothing for empty text', () => {
    const { engine } = fakeEngine();
    const speech = createSpeech(() => engine);

    expect(speech.speak('  ', 'en')).toBe(false);
    expect(engine.speak).not.toHaveBeenCalled();
  });

  it('gives way to an alarm, and stays quiet while it rings', () => {
    vi.useFakeTimers();
    const { engine } = fakeEngine();
    const speech = createSpeech(() => engine);

    speech.speak('Stir.', 'en');
    speech.interrupt();

    expect(speech.speaking).toBe(false);
    expect(speech.speak('Serve.', 'en')).toBe(false);

    vi.advanceTimersByTime(1600);

    expect(speech.speak('Serve.', 'en')).toBe(true);
  });

  it('is absent without a speech engine', () => {
    const speech = createSpeech(() => undefined);

    expect(speech.supported).toBe(false);
    expect(speech.speak('Stir.', 'en')).toBe(false);
    expect(() => speech.cancel()).not.toThrow();
    expect(() => speech.interrupt()).not.toThrow();
  });

  it('is absent without utterances, as in old WebViews', () => {
    vi.stubGlobal('SpeechSynthesisUtterance', undefined);
    const { engine } = fakeEngine();

    expect(createSpeech(() => engine).supported).toBe(false);
  });

  it('remembers auto-read on this device', () => {
    const { engine } = fakeEngine();
    const first = createSpeech(() => engine);

    first.setAutoRead(true);

    const second = createSpeech(() => engine);
    expect(second.autoRead).toBe(false);

    second.load();
    expect(second.autoRead).toBe(true);
  });

  it('copes with storage that is blocked', () => {
    const { engine } = fakeEngine();
    const speech = createSpeech(() => engine);
    const blocked = vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('blocked');
    });

    expect(() => speech.setAutoRead(true)).not.toThrow();
    expect(speech.autoRead).toBe(true);
    blocked.mockRestore();
  });
});
