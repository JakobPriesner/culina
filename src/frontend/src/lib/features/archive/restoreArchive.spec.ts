import { afterEach, describe, expect, it, vi } from 'vitest';

import { restoreArchive } from './restoreArchive';

const file = new File(['{}'], 'culina.json', { type: 'application/json' });

afterEach(() => vi.unstubAllGlobals());

describe('restoring an archive', () => {
  it('reports what was restored, even when the server takes longer than an ordinary call', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(
        () =>
          new Promise<Response>((resolve) =>
            setTimeout(
              () =>
                resolve(
                  new Response(JSON.stringify({ restored: 4, skipped: 1 }), {
                    status: 200,
                    headers: { 'Content-Type': 'application/json' }
                  })
                ),
              30
            )
          )
      )
    );

    const result = await restoreArchive('h1', file);

    expect(result).toEqual({ ok: true, value: { restored: 4, skipped: 1 } });
  });

  it('keeps the server problem, so the reason and request id can be shown', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(() =>
        Promise.resolve(
          new Response(
            JSON.stringify({ code: 'archive.not_an_archive', detail: 'no', requestId: 'req-9' }),
            { status: 422, headers: { 'Content-Type': 'application/problem+json' } }
          )
        )
      )
    );

    const result = await restoreArchive('h1', file);

    expect(result.ok).toBe(false);
    expect(!result.ok && result.error.code).toBe('archive.not_an_archive');
    expect(!result.ok && result.error.requestId).toBe('req-9');
  });
});
