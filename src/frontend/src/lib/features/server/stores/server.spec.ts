import { beforeEach, describe, expect, it, vi } from 'vitest';

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
