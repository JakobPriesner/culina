import type { BarcodeFormat } from 'barcode-detector/ponyfill';
// A URL into the app's own build (`/_app/…`); the file itself is fetched only by the fallback decoder.
import readerWasm from 'zxing-wasm/reader/zxing_reader.wasm?url';

/**
 * The camera and the decoder behind the scan sheet (`docs/barcode-scanning-design.md` §6). Everything here talks to the
 * browser; the sheet only hears "locked on this code". `detect` is the one seam where a decoder plugs in: the browser's
 * own `BarcodeDetector` where there is one, and ZXing compiled to WebAssembly where there is not (§5.1).
 */

/** Reads one video frame: the digits of the barcode in it, or null when there is none. */
export type Detect = (video: HTMLVideoElement) => Promise<string | null>;

/** Why the camera could not be used; typing the digits stays possible in every case. */
export type CameraFailure = 'denied' | 'none';

export interface Scanner {
  /** Whether the camera can switch its light on; Chrome on Android can, Safari cannot. */
  readonly hasTorch: boolean;
  setTorch(on: boolean): Promise<void>;
  /** Stops decoding and releases the camera. */
  stop(): void;
}

export type Started =
  | { readonly ok: true; readonly scanner: Scanner }
  | { readonly ok: false; readonly reason: CameraFailure };

interface NativeDetector {
  detect(source: HTMLVideoElement): Promise<readonly { rawValue: string }[]>;
}

type NativeDetectorConstructor = new (options: { formats: string[] }) => NativeDetector;

/** What groceries carry; QR is out, no grocery identity lives in one. */
const formats: BarcodeFormat[] = ['ean_13', 'ean_8', 'upc_a', 'upc_e'];

/** Decoding stays at about eight attempts a second: a barcode needs width, not speed, and a mid-range phone keeps up. */
const frameGapMs = 125;

/** The browser's own decoder, or null where it has none (every iPhone, Firefox). */
export function nativeDetector(): Detect | null {
  const Constructor = (globalThis as { BarcodeDetector?: NativeDetectorConstructor })
    .BarcodeDetector;

  if (!Constructor || !navigator.mediaDevices?.getUserMedia) {
    return null;
  }

  const detector = new Constructor({ formats });

  return async (video) => (await detector.detect(video))[0]?.rawValue ?? null;
}

/**
 * ZXing compiled to WebAssembly, for browsers without a decoder of their own. It is imported only here, so it never
 * weighs on a cold load, and its .wasm comes from the app's own build: the library would otherwise fetch it from a CDN.
 * Null where there is no camera, or where the decoder cannot load (offline before it was ever fetched, or a policy
 * without 'wasm-unsafe-eval').
 */
export async function fallbackDetector(): Promise<Detect | null> {
  if (!navigator.mediaDevices?.getUserMedia) {
    return null;
  }

  try {
    // The ponyfill bundles its own copy of zxing-wasm's code, so the module is prepared through it, not through zxing-wasm.
    const { BarcodeDetector, prepareZXingModule } = await import('barcode-detector/ponyfill');

    // Compiled now rather than on the first frame, so a decoder that cannot load shows as one before the camera opens.
    await prepareZXingModule({
      overrides: {
        locateFile: (file: string, prefix: string) =>
          file.endsWith('.wasm') ? readerWasm : prefix + file
      },
      fireImmediately: true
    });

    const detector = new BarcodeDetector({ formats });

    return async (video) => (await detector.detect(video))[0]?.rawValue ?? null;
  } catch {
    return null;
  }
}

/** The browser's own decoder first, then ZXing; null means the digits are typed. */
export async function chooseDetector(): Promise<Detect | null> {
  return nativeDetector() ?? (await fallbackDetector());
}

/**
 * Two equal reads in a row make a lock, so a half-visible code is never taken for another; a frame with no code, or
 * a different one, starts over. Answers the code on the second read, and null until then.
 */
export function createLock(): (read: string | null) => string | null {
  let previous: string | null = null;

  return (read) => {
    const locked = read !== null && read === previous ? read : null;

    previous = locked === null ? read : null;

    return locked;
  };
}

/**
 * Opens the rear camera at 720p in `video` and reports a code once it is locked. The caller stops the scanner when it
 * has what it wanted, and starts a new one to scan again, so a result being shown is never read over.
 */
export async function startScanner(
  video: HTMLVideoElement,
  detect: Detect,
  onlock: (code: string) => void
): Promise<Started> {
  let stream: MediaStream;

  try {
    stream = await navigator.mediaDevices.getUserMedia({
      video: {
        facingMode: { ideal: 'environment' },
        width: { ideal: 1280 },
        height: { ideal: 720 }
      },
      audio: false
    });
  } catch (thrown) {
    return {
      ok: false,
      reason:
        thrown instanceof DOMException && thrown.name === 'NotAllowedError' ? 'denied' : 'none'
    };
  }

  video.srcObject = stream;
  video.muted = true;

  try {
    await video.play();
  } catch {
    // Autoplay is refused only before the user did anything; the frames start when it plays.
  }

  const track = stream.getVideoTracks()[0];
  const hasTorch = Boolean((track?.getCapabilities?.() as { torch?: boolean } | undefined)?.torch);
  const lock = createLock();

  let stopped = false;
  let reading = false;
  let timer: ReturnType<typeof setTimeout> | undefined;
  let lastAt = 0;

  const next = () => {
    if (stopped) {
      return;
    }

    if (typeof video.requestVideoFrameCallback === 'function') {
      video.requestVideoFrameCallback(frame);
    } else {
      timer = setTimeout(frame, frameGapMs);
    }
  };

  const frame = () => {
    if (stopped) {
      return;
    }

    const now = performance.now();

    // Not while the tab is hidden, while the last frame is still being read, or faster than the gap.
    if (document.hidden || reading || now - lastAt < frameGapMs) {
      next();

      return;
    }

    lastAt = now;
    reading = true;

    detect(video)
      .then((read) => {
        const locked = lock(read);

        if (locked !== null && !stopped) {
          onlock(locked);
        }
      })
      .catch(() => {
        // A frame that cannot be read is a frame without a code.
        lock(null);
      })
      .finally(() => {
        reading = false;
        next();
      });
  };

  next();

  return {
    ok: true,
    scanner: {
      hasTorch,
      async setTorch(on) {
        await track?.applyConstraints({ advanced: [{ torch: on } as MediaTrackConstraintSet] });
      },
      stop() {
        stopped = true;
        clearTimeout(timer);

        for (const each of stream.getTracks()) {
          each.stop();
        }

        video.srcObject = null;
      }
    }
  };
}
