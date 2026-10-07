import { describe, expect, it } from 'vitest';

import { WriteQueue } from './writeQueue';

const deferred = () => {
  let resolve!: () => void;
  const promise = new Promise<void>((done) => (resolve = done));

  return { promise, resolve };
};

describe('WriteQueue', () => {
  it('runs writes for one key one after another', async () => {
    const queue = new WriteQueue();
    const order: string[] = [];
    const gate = deferred();

    const first = queue.run('a', async () => {
      order.push('first:start');
      await gate.promise;
      order.push('first:end');
    });
    const second = queue.run('a', async () => {
      order.push('second:start');
    });

    await Promise.resolve();
    expect(order).toEqual(['first:start']);

    gate.resolve();
    await Promise.all([first, second]);

    expect(order).toEqual(['first:start', 'first:end', 'second:start']);
  });

  it('does not make different keys wait for each other', async () => {
    const queue = new WriteQueue();
    const gate = deferred();
    let ran = false;

    const slow = queue.run('a', () => gate.promise);

    await queue.run('b', async () => {
      ran = true;
    });

    expect(ran).toBe(true);
    gate.resolve();
    await slow;
  });

  it('still runs a write after the one before it failed', async () => {
    const queue = new WriteQueue();

    const failed = queue.run('a', () => Promise.reject(new Error('nope')));
    const next = queue.run('a', async () => 'ok');

    await expect(failed).rejects.toThrow('nope');
    await expect(next).resolves.toBe('ok');
  });

  it('returns what the write returned', async () => {
    await expect(new WriteQueue().run('a', async () => 42)).resolves.toBe(42);
  });
});
