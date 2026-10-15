import { afterEach, describe, expect, it, vi } from 'vitest';

import { server } from './stores/server.svelte';
import { useServerDrafts } from './useServerDrafts.svelte';

/* The copies both server screens edit: typing never reaches the store, and an unreadable number is not sent. */
afterEach(() => {
  vi.restoreAllMocks();
  server.clear();
});

describe('the server drafts', () => {
  it('copies what the store loaded, so edits stay in the draft', async () => {
    vi.spyOn(server, 'loadServer').mockResolvedValue();
    vi.spyOn(server, 'server', 'get').mockReturnValue({ secure: true } as never);

    const drafts = useServerDrafts();

    await drafts.loadServer();
    drafts.serverDraft!.secure = false;

    expect(server.server!.secure).toBe(true);
  });

  it('keeps the outcome of a save and reports nothing to send when there is no draft', async () => {
    const drafts = useServerDrafts();

    expect(await drafts.saveServer()).toBeNull();

    vi.spyOn(server, 'awaitRestart').mockResolvedValue({ kind: 'stalled' });

    expect(await drafts.retry('database')).toEqual({ kind: 'stalled' });
    expect(drafts.databaseOutcome).toEqual({ kind: 'stalled' });

    drafts.dismissOutcome('database');

    expect(drafts.databaseOutcome).toBeNull();
  });
});
