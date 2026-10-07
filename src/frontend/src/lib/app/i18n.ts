import {
  baseLocale,
  defineCustomClientStrategy,
  extractLocaleFromNavigator,
  getLocale,
  locales,
  type Locale
} from '$lib/paraglide/runtime';

import { readDevice, writeDevice } from './deviceStorage';

/**
 * Everything for saying something in the reader's language; messages compile to functions so a missing key fails the build.
 * **Append new keys to `messages/*.json`; never re-sort:** the compiler can fail with "No Lix transaction is active" and even emit correct output while exiting non-zero.
 */
export { m } from '$lib/paraglide/messages';
export { locales, type Locale };

/** What a person chose. `system` is a choice: read whatever the device reads. */
export type LocaleChoice = Locale | 'system';

export const isLocale = (value: unknown): value is Locale =>
  typeof value === 'string' && (locales as readonly string[]).includes(value);

export const isLocaleChoice = (value: unknown): value is LocaleChoice =>
  value === 'system' || isLocale(value);

/** Where this device remembers the choice, `system` included. */
const choiceKey = 'culina.locale';

/** First link of the Paraglide chain in `vite.config.ts`: device choice, device language, English. Paraglide only reads it; the preferences store writes it because `system` is not a locale. */
defineCustomClientStrategy('custom-choice', {
  getLocale: () => {
    const chosen = readDevice(choiceKey);

    return isLocale(chosen) ? chosen : undefined;
  },
  setLocale: () => {}
});

/** The choice this device remembers; `system` when it remembers none. */
export function rememberedLocale(): LocaleChoice {
  const chosen = readDevice(choiceKey);

  return isLocaleChoice(chosen) ? chosen : 'system';
}

/** Switches language without a reload and remembers it on this device. */
export function applyLocale(choice: LocaleChoice): void {
  writeDevice(choiceKey, choice);
}

/** The first language the device asks for that Culina speaks, or English. */
export const deviceLocale = (): Locale => extractLocaleFromNavigator() ?? baseLocale;

/** Intl formatters, made once per locale: constructing one is slow enough to matter across hundreds of rows, and they are immutable. */
const numberFormats = new Map<string, Intl.NumberFormat>();
const dateFormats = new Map<string, Intl.DateTimeFormat>();
const listFormats = new Map<string, Intl.ListFormat>();

/** `1.5` in English, `1,5` in German. */
export function formatNumber(value: number, options: Intl.NumberFormatOptions = {}): string {
  return formatter(
    numberFormats,
    options,
    (locale) => new Intl.NumberFormat(locale, options)
  ).format(value);
}

export function formatDate(value: Date, options: Intl.DateTimeFormatOptions = {}): string {
  return formatter(
    dateFormats,
    options,
    (locale) => new Intl.DateTimeFormat(locale, options)
  ).format(value);
}

/** "Hackfleisch, Tomate und Zwiebel" — a list joined the way the reader's language joins one. */
export function formatList(values: readonly string[]): string {
  return formatter(
    listFormats,
    {},
    (locale) => new Intl.ListFormat(locale, { style: 'long', type: 'conjunction' })
  ).format(values);
}

function formatter<TFormat>(
  cache: Map<string, TFormat>,
  options: object,
  create: (locale: string) => TFormat
): TFormat {
  const locale = getLocale();
  const key = `${locale}:${JSON.stringify(options)}`;
  const existing = cache.get(key);

  if (existing) {
    return existing;
  }

  const made = create(locale);

  cache.set(key, made);

  return made;
}
