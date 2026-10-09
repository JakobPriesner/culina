import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { toDatabaseDraft } from '../types';
import { server } from './server.svelte';

const json = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' }
  });

const database = {
  host: 'db',
  port: 5432,
  name: 'culina',
  username: 'culina',
  requireSsl: false,
  maxPoolSize: 20,
  pinned: [],
  writable: true,
  passwordConfigured: true
};

beforeEach(() => server.clear());

describe('reading both groups for the settings screen', () => {
  it('fails when the server settings fail, even if the database answers later', async () => {
    let answerDatabase = () => {};

    vi.stubGlobal(
      'fetch',
      vi.fn((input: Request) =>
        input.url.endsWith('/settings/server')
          ? Promise.resolve(json({ code: 'server.error' }, 500))
          : new Promise<Response>((resolve) => (answerDatabase = () => resolve(json(database))))
      )
    );

    const loading = server.load();
    await vi.waitFor(() => expect(server.error).not.toBeNull());
    answerDatabase();
    await loading;

    // The database read succeeding says nothing about the server read: a
    // "ready" here left the screen on its skeleton with nothing to retry.
    expect(server.status).toBe('failed');
    expect(server.server).toBeNull();
  });
});

describe('waiting for a restart', () => {
  const setup = (startedAt: string) => json({ stage: 'complete', startedAt });

  // Answers GET /setup from the queue, then keeps repeating the last one; the PUT is a 202.
  function stubRestart(...startedAt: (string | null)[]) {
    let reads = 0;

    vi.stubGlobal(
      'fetch',
      vi.fn((input: Request) => {
        if (input.method === 'PUT') {
          return Promise.resolve(new Response(null, { status: 202 }));
        }

        const next = startedAt[Math.min(reads++, startedAt.length - 1)] ?? null;

        return Promise.resolve(next === null ? json({ code: 'x' }, 502) : setup(next));
      })
    );
  }

  beforeEach(() => vi.useFakeTimers());

  afterEach(() => vi.useRealTimers());

  it('checks again against the host from before the save, not the first answer', async () => {
    stubRestart('old');

    const saving = server.saveDatabase(toDatabaseDraft(database));
    await vi.advanceTimersByTimeAsync(91_000);
    expect(await saving).toEqual({ kind: 'stalled' });

    const again = server.awaitRestart();
    await vi.advanceTimersByTimeAsync(91_000);

    expect(await again).toEqual({ kind: 'stalled' });
  });

  it('applies once a new host answers while checking again', async () => {
    stubRestart('old');

    const saving = server.saveDatabase(toDatabaseDraft(database));
    await vi.advanceTimersByTimeAsync(91_000);
    await saving;

    stubRestart('new');

    const again = server.awaitRestart();
    await vi.advanceTimersByTimeAsync(1000);

    expect(await again).toMatchObject({
      kind: 'applied',
      setup: { startedAt: 'new' }
    });
  });

  it('cannot tell without the host from before, so it is a stall', async () => {
    stubRestart(null, 'new');

    expect(await server.saveDatabase(toDatabaseDraft(database))).toEqual({ kind: 'stalled' });
    expect(await server.awaitRestart()).toEqual({ kind: 'stalled' });
  });
});
