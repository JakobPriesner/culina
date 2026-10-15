import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { imageSource, releaseImage } from '$ds/display/imageSources';

const fetchFile = vi.fn<(url: string) => Promise<Blob | null>>();

vi.mock('$api', () => ({ fetchFile: (url: string) => fetchFile(url) }));

const { forgetNativeImages, startNativeImages } = await import('./nativeImages');

const revoked: string[] = [];
let made = 0;

beforeEach(() => {
  revoked.length = 0;
  made = 0;
  fetchFile.mockReset();
  vi.stubGlobal('URL', {
    createObjectURL: () => `blob:${++made}`,
    revokeObjectURL: (url: string) => revoked.push(url)
  });
  startNativeImages();
});

afterEach(() => {
  forgetNativeImages();
  vi.unstubAllGlobals();
});

describe('the app’s photo source', () => {
  it('fetches again after a failure instead of answering "none" until sign-out', async () => {
    fetchFile.mockResolvedValueOnce(null).mockResolvedValueOnce(new Blob(['x']));

    const resolve = imageSource()!;

    expect(await resolve('/api/v1/a')).toBeNull();
    expect(await resolve('/api/v1/a')).toBe('blob:1');
    expect(fetchFile).toHaveBeenCalledTimes(2);
  });

  it('does not revoke a photo that is still showing when the cache overflows', async () => {
    fetchFile.mockResolvedValue(new Blob(['x']));

    const resolve = imageSource()!;
    const onScreen = await resolve('/api/v1/first');

    for (let index = 0; index < 160; index++) {
      const url = `/api/v1/scrolled-${index}`;

      await resolve(url);
      releaseImage(url);
    }

    expect(revoked).not.toContain(onScreen);
    expect(revoked.length).toBeGreaterThan(0);

    // Once it stops showing, the next overflow may release it.
    releaseImage('/api/v1/first');
    await resolve('/api/v1/one-more');

    expect(revoked).toContain(onScreen);
  });
});
