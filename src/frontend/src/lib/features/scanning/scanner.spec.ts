import { readFileSync } from 'node:fs';

import { afterEach, describe, expect, it, vi } from 'vitest';

import {
  chooseDetector,
  createLock,
  fallbackDetector,
  nativeDetector,
  startScanner
} from './scanner';

const ponyfill = vi.hoisted(() => ({
  formats: [] as unknown[],
  prepared: vi.fn<(options: unknown) => Promise<unknown>>(),
  read: '4008400402222'
}));

// The ZXing ponyfill stands in for the real one: jsdom cannot compile its WebAssembly, and only the choice is tested here.
vi.mock('barcode-detector/ponyfill', () => ({
  prepareZXingModule: ponyfill.prepared,
  BarcodeDetector: class {
    constructor(options: { formats: unknown }) {
      ponyfill.formats.push(options.formats);
    }

    detect = async () => [{ rawValue: ponyfill.read }];
  }
}));

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

describe('choosing a decoder', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    ponyfill.formats = [];
    ponyfill.prepared.mockReset();
  });

  const camera = () => vi.stubGlobal('navigator', { mediaDevices: { getUserMedia: vi.fn() } });
  const video = () => document.createElement('video');

  it('takes the browser’s own decoder where there is one, without loading ZXing', async () => {
    camera();
    vi.stubGlobal(
      'BarcodeDetector',
      class {
        detect = async () => [{ rawValue: '8076800195057' }];
      }
    );

    const detect = await chooseDetector();

    expect(await detect?.(video())).toBe('8076800195057');
    expect(ponyfill.prepared).not.toHaveBeenCalled();
  });

  it('falls back to ZXing for the same formats, its WebAssembly from the app’s own build', async () => {
    camera();
    ponyfill.prepared.mockResolvedValue({});

    const detect = await chooseDetector();

    expect(await detect?.(video())).toBe('4008400402222');
    expect(ponyfill.formats).toEqual([['ean_13', 'ean_8', 'upc_a', 'upc_e']]);

    const [{ overrides }] = ponyfill.prepared.mock.calls[0] as [
      { overrides: { locateFile: (file: string, prefix: string) => string } }
    ];
    const located = overrides.locateFile('zxing_reader.wasm', 'https://fastly.jsdelivr.net/');

    expect(located).toMatch(/^\/.*zxing_reader.*\.wasm$/);
  });

  it('leaves the digits to be typed where ZXing cannot load', async () => {
    camera();
    ponyfill.prepared.mockRejectedValue(new WebAssembly.CompileError('blocked by the policy'));

    expect(await chooseDetector()).toBeNull();
  });

  it('leaves the digits to be typed where there is no camera at all', async () => {
    expect(await fallbackDetector()).toBeNull();
    expect(ponyfill.prepared).not.toHaveBeenCalled();
  });

  it('ships the WebAssembly the ponyfill was built against, since a mismatch fails only on iPhones', () => {
    const wanted = (path: string) =>
      (JSON.parse(readFileSync(path, 'utf8')) as { dependencies: Record<string, string> })
        .dependencies['zxing-wasm'];

    expect(wanted('package.json')).toBe(wanted('node_modules/barcode-detector/package.json'));
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
