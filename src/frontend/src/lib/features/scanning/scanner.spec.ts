import { afterEach, describe, expect, it, vi } from 'vitest';

import { createLock, nativeDetector, startScanner } from './scanner';

describe('locking on a code', () => {
  it('takes a code on its second equal read in a row', () => {
    const lock = createLock();

    expect(lock('8076800195057')).toBeNull();
    expect(lock('8076800195057')).toBe('8076800195057');
  });

  it('starts over on a different code or a frame with none, so a half-visible code is never taken for another', () => {
    const lock = createLock();

    expect(lock('8076800195057')).toBeNull();
    expect(lock('4008400402222')).toBeNull();
    expect(lock(null)).toBeNull();
    expect(lock('4008400402222')).toBeNull();
    expect(lock('4008400402222')).toBe('4008400402222');
  });
});

describe('the browser’s own decoder', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('is absent where the browser has none, which is where the digits are typed', () => {
    expect(nativeDetector()).toBeNull();
  });

  it('asks for the formats groceries carry and reads the first code in a frame', async () => {
    const formats: string[][] = [];

    vi.stubGlobal(
      'BarcodeDetector',
      class {
        constructor(options: { formats: string[] }) {
          formats.push(options.formats);
        }

        detect = async () => [{ rawValue: '8076800195057' }];
      }
    );
    vi.stubGlobal('navigator', { mediaDevices: { getUserMedia: vi.fn() } });

    const detect = nativeDetector();

    expect(formats).toEqual([['ean_13', 'ean_8', 'upc_a', 'upc_e']]);
    expect(await detect?.(document.createElement('video'))).toBe('8076800195057');
  });
});

describe('opening the camera', () => {
  afterEach(() => vi.unstubAllGlobals());

  const media = (getUserMedia: () => Promise<unknown>) =>
    vi.stubGlobal('navigator', { mediaDevices: { getUserMedia } });

  it('says the camera is blocked when the person refused it', async () => {
    media(() => Promise.reject(new DOMException('no', 'NotAllowedError')));

    const started = await startScanner(
      document.createElement('video'),
      async () => null,
      () => {}
    );

    expect(started).toEqual({ ok: false, reason: 'denied' });
  });

  it('says there is no camera when the device has none', async () => {
    media(() => Promise.reject(new DOMException('none', 'NotFoundError')));

    const started = await startScanner(
      document.createElement('video'),
      async () => null,
      () => {}
    );

    expect(started).toEqual({ ok: false, reason: 'none' });
  });

  it('offers the light only where the camera can switch it on, and releases the camera on stop', async () => {
    const stopped = vi.fn();
    const track = {
      getCapabilities: () => ({ torch: true }),
      stop: stopped,
      applyConstraints: vi.fn()
    };

    media(() =>
      Promise.resolve({
        getVideoTracks: () => [track],
        getTracks: () => [track]
      } as unknown as MediaStream)
    );

    const video = document.createElement('video');

    video.play = () => Promise.resolve();

    const started = await startScanner(
      video,
      async () => null,
      () => {}
    );

    expect(started.ok && started.scanner.hasTorch).toBe(true);

    if (started.ok) {
      await started.scanner.setTorch(true);
      started.scanner.stop();
    }

    expect(track.applyConstraints).toHaveBeenCalledWith({ advanced: [{ torch: true }] });
    expect(stopped).toHaveBeenCalled();
  });
});
