import { flushSync } from 'svelte';
import { describe, expect, it, vi } from 'vitest';

import { LruCache } from './lruCache.svelte';

describe('LruCache', () => {
  it('keeps what was put in, up to the limit', () => {
    const cache = new LruCache<number>(3);

    cache.set('a', 1);
    cache.set('b', 2);
    cache.set('c', 3);

    expect([cache.peek('a'), cache.peek('b'), cache.peek('c')]).toEqual([1, 2, 3]);
    expect(cache.size).toBe(3);
  });

  it('drops the entry used least recently, and says which', () => {
    const evicted = vi.fn();
    const cache = new LruCache<number>(2, evicted);

    cache.set('a', 1);
    cache.set('b', 2);
    cache.set('c', 3);

    expect(cache.peek('a')).toBeUndefined();
    expect(cache.size).toBe(2);
    expect(evicted).toHaveBeenCalledExactlyOnceWith('a');
  });

  it('counts a read as use, so what is on screen stays', () => {
    const cache = new LruCache<number>(2);

    cache.set('a', 1);
    cache.set('b', 2);
    cache.get('a');
    cache.set('c', 3);

    expect(cache.peek('a')).toBe(1);
    expect(cache.peek('b')).toBeUndefined();
  });

  it('does not count a peek as use', () => {
    const cache = new LruCache<number>(2);

    cache.set('a', 1);
    cache.set('b', 2);
    cache.peek('a');
    cache.set('c', 3);

    expect(cache.peek('a')).toBeUndefined();
  });

  it('counts a write to an existing key as use and does not grow', () => {
    const cache = new LruCache<number>(2);

    cache.set('a', 1);
    cache.set('b', 2);
    cache.set('a', 10);
    cache.set('c', 3);

    expect(cache.peek('a')).toBe(10);
    expect(cache.peek('b')).toBeUndefined();
    expect(cache.size).toBe(2);
  });

  it('changes every entry and keeps the order', () => {
    const cache = new LruCache<number>(2);

    cache.set('a', 1);
    cache.set('b', 2);
    cache.update((value) => value * 10);
    cache.set('c', 3);

    expect(cache.peek('a')).toBeUndefined();
    expect(cache.peek('b')).toBe(20);
  });

  it('puts a snapshot back exactly', () => {
    const cache = new LruCache<number>(3);

    cache.set('a', 1);

    const before = cache.snapshot();

    cache.set('b', 2);
    cache.update((value) => value + 1);
    cache.restore(before);

    expect(cache.peek('a')).toBe(1);
    expect(cache.peek('b')).toBeUndefined();
  });

  it('tells a reader that read through get when anything is written', () => {
    const cache = new LruCache<number>(2);
    const seen: (number | undefined)[] = [];

    const stop = $effect.root(() => {
      $effect(() => {
        seen.push(cache.get('a'));
      });
    });

    flushSync();
    cache.set('a', 1);
    flushSync();
    cache.set('b', 2);
    cache.set('c', 3);
    flushSync();
    stop();

    expect(seen).toEqual([undefined, 1, undefined]);
  });

  it('does not wake a reader that only peeked', () => {
    const cache = new LruCache<number>(2);
    const seen: (number | undefined)[] = [];

    const stop = $effect.root(() => {
      $effect(() => {
        seen.push(cache.peek('a'));
      });
    });

    flushSync();
    cache.set('a', 1);
    flushSync();
    stop();

    expect(seen).toEqual([undefined]);
  });
});
