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
 * Everything the app needs to say something in the reader's language.
 *
 * Messages are compiled to functions, so a key that does not exist fails the
 * build instead of rendering an empty string in production. Nothing in a
 * component is ever a literal string a person can read.
 *
 * **Append new keys to `messages/*.json`; never re-sort the files.** The
 * compiler falls over with "No Lix transaction is active" on some reorderings,
 * and — worse — it can emit correct output *and* exit non-zero, so a working
 * app is not evidence that the build passed. Appending has always worked.
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

/**
 * The first link in the chain `vite.config.ts` gives Paraglide: the choice on
 * this device, then the device's own language, then English. A signed-in
 * person's server setting replaces the choice as soon as it arrives.
 *
 * Paraglide reads the choice but never writes it — the preferences store does
 * — because `system` is not a locale, and Paraglide's own storage would
 * replace it with whatever the device happened to read that day.
 */
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

/**
 * Switches language without a reload — the next message rendered reads the
 * choice — and remembers it on this device.
 */
export function applyLocale(choice: LocaleChoice): void {
  writeDevice(choiceKey, choice);
}

/** The first language the device asks for that Culina speaks, or English. */
export const deviceLocale = (): Locale => extractLocaleFromNavigator() ?? baseLocale;

/**
 * Intl formatters, made once per locale.
 *
 * Constructing one is expensive enough that doing it inside a list of two
 * hundred ingredient rows is measurable, and the result is immutable, so it is
 * cached.
 */
const numberFormats = new Map<string, Intl.NumberFormat>();
const dateFormats = new Map<string, Intl.DateTimeFormat>();
const listFormats = new Map<string, Intl.ListFormat>();

/** `1.5` in English, `1,5` in German — the same number, read correctly. */
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
