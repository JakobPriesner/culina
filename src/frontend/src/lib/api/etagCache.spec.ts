import { beforeEach, describe, expect, it } from 'vitest';

import { cached, forgetEverything, invalidate, remember } from './etagCache';

beforeEach(forgetEverything);

describe('invalidate', () => {
  it('forgets the url, what hangs off it, and the list it appears in', () => {
    remember('/api/v1/recipes', '"list"', '[]');
    remember('/api/v1/recipes/x', '"x"', '{}');
    remember('/api/v1/recipes/x/notes?page=2', '"notes"', '[]');
    remember('/api/v1/cookbooks', '"books"', '[]');

    invalidate('/api/v1/recipes/x');

    expect(cached('/api/v1/recipes')).toBeUndefined();
    expect(cached('/api/v1/recipes/x')).toBeUndefined();
    expect(cached('/api/v1/recipes/x/notes?page=2')).toBeUndefined();
    expect(cached('/api/v1/cookbooks')?.etag).toBe('"books"');
  });
});

describe('capacity', () => {
  it('drops the entry read least recently once it is full', () => {
    for (let index = 0; index < 100; index++) {
      remember(`/api/v1/things/${index}`, `"${index}"`, '');
    }

    // Reading the oldest makes it the newest, so the next one is the victim.
    expect(cached('/api/v1/things/0')).toBeDefined();
    remember('/api/v1/things/100', '"100"', '');

    expect(cached('/api/v1/things/0')).toBeDefined();
    expect(cached('/api/v1/things/1')).toBeUndefined();
    expect(cached('/api/v1/things/100')).toBeDefined();
  });

  it('replaces an entry for the same url instead of growing', () => {
    remember('/api/v1/things/a', '"1"', 'first');
    remember('/api/v1/things/a', '"2"', 'second');

    expect(cached('/api/v1/things/a')).toMatchObject({ etag: '"2"', body: 'second' });
  });
});
