import { readFile } from 'node:fs/promises';
import { describe, expect, it } from 'vitest';

import { locales, m } from './i18n';

/*
 * A message that exists in one language and not the other is not a compile
 * error — Paraglide falls back to the base locale — so the reader simply gets
 * English in the middle of a German page. That is what this catches.
 */
type Message =
  string | { declarations: string[]; selectors: string[]; match: Record<string, string> }[];
const variants = (message: Message): Record<string, string> =>
  typeof message === 'string'
    ? { default: message }
    : Object.assign({}, ...message.map((item) => item.match));

const load = async (locale: string): Promise<Record<string, Message>> => {
  const contents: unknown = JSON.parse(await readFile(`messages/${locale}.json`, 'utf8'));
  const { $schema: _schema, ...messages } = contents as Record<string, Message>;

  return messages;
};

const catalogues = Object.fromEntries(
  await Promise.all(locales.map(async (locale) => [locale, await load(locale)] as const))
) as Record<string, Record<string, Message>>;

const base = locales[0]!;

describe('the message catalogues', () => {
  it('are not empty', () => {
    expect(Object.keys(catalogues[base]!).length).toBeGreaterThan(0);
  });

  it.each(locales.filter((locale) => locale !== base))(
    'give %s exactly the keys the base locale has',
    (locale) => {
      expect(Object.keys(catalogues[locale]!).sort()).toEqual(
        Object.keys(catalogues[base]!).sort()
      );
    }
  );

  it.each(locales)('leave no message in %s empty', (locale) => {
    const blank = Object.entries(catalogues[locale]!)
      .filter(
        ([, value]) =>
          Object.values(variants(value)).length === 0 ||
          Object.values(variants(value)).some((text) => text.trim() === '')
      )
      .map(([key]) => key);

    expect(blank).toEqual([]);
  });

  it.each(locales.filter((locale) => locale !== base))(
    'interpolate the same placeholders in %s as in the base locale',
    (locale) => {
      const placeholders = (value: string) =>
        [...value.matchAll(/\{(\w+)\}/g)].map(([, name]) => name).sort();

      for (const [key, value] of Object.entries(catalogues[base]!)) {
        const translated = variants(catalogues[locale]![key] ?? '');
        const original = variants(value);
        expect(Object.keys(translated).sort(), key).toEqual(Object.keys(original).sort());
        for (const [selector, text] of Object.entries(original)) {
          expect(placeholders(translated[selector] ?? ''), `${key}: ${selector}`).toEqual(
            placeholders(text)
          );
        }
      }
    }
  );

  it('namespaces every key by feature', () => {
    const unnamespaced = Object.keys(catalogues[base]!).filter((key) => !key.includes('.'));

    expect(unnamespaced, 'a key like `title` collides the moment a second page wants one').toEqual(
      []
    );
  });
});

describe('counted library copy', () => {
  it.each(['en', 'de'] as const)('uses singular and plural forms in %s', (locale) => {
    for (const key of ['recipes.list.count', 'cookbooks.card.count', 'preview.count'] as const) {
      expect(m[key]({ count: 1 }, { locale })).toBe(locale === 'en' ? '1 recipe' : '1 Rezept');
      expect(m[key]({ count: 0 }, { locale })).toBe(locale === 'en' ? '0 recipes' : '0 Rezepte');
      expect(m[key]({ count: 2 }, { locale })).toBe(locale === 'en' ? '2 recipes' : '2 Rezepte');
    }
    expect(m['recipes.meta.servings']({ count: 1 }, { locale })).toBe(
      locale === 'en' ? '1 serving' : '1 Portion'
    );
  });
});
