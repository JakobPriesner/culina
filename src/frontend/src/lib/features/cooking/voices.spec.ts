import { describe, expect, it } from 'vitest';

import { pickVoice } from './voices';

const voice = (
  name: string,
  lang: string,
  options: { localService?: boolean; default?: boolean } = {}
) =>
  ({
    name,
    lang,
    voiceURI: name,
    localService: options.localService ?? true,
    default: options.default ?? false
  }) as SpeechSynthesisVoice;

describe('pickVoice', () => {
  it('takes a voice of the language, whatever its region', () => {
    const voices = [voice('Samantha', 'en-US'), voice('Anna', 'de_DE')];

    expect(pickVoice(voices, 'de')?.name).toBe('Anna');
  });

  it('prefers premium, enhanced, neural and network voices', () => {
    const voices = [
      voice('Anna', 'de-DE'),
      voice('Anna (Enhanced)', 'de-DE'),
      voice('Google Deutsch', 'de-DE', { localService: false })
    ];

    expect(pickVoice(voices, 'de')?.name).toBe('Anna (Enhanced)');
    expect(pickVoice(voices.slice(0, 1).concat(voices.slice(2)), 'de')?.name).toBe(
      'Google Deutsch'
    );
  });

  it('avoids compact, eSpeak and novelty voices', () => {
    const voices = [
      voice('Zarvox', 'en-US'),
      voice('eSpeak English', 'en'),
      voice('Samantha', 'en-US')
    ];

    expect(pickVoice(voices, 'en')?.name).toBe('Samantha');
  });

  it('prefers an offline voice, then the default, then the first listed', () => {
    expect(
      pickVoice([voice('A', 'en', { localService: false }), voice('B', 'en')], 'en')?.name
    ).toBe('B');
    expect(pickVoice([voice('A', 'en'), voice('B', 'en', { default: true })], 'en')?.name).toBe(
      'B'
    );
    expect(pickVoice([voice('A', 'en'), voice('B', 'en')], 'en')?.name).toBe('A');
  });

  it('has none when the device speaks nothing of the language', () => {
    expect(pickVoice([voice('Samantha', 'en-US')], 'de')).toBeUndefined();
    expect(pickVoice([], 'en')).toBeUndefined();
  });
});
