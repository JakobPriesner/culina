import { fetchFile } from '$api';
import { useImageSource } from '$ds/display/imageSources';

/** Photos kept as object URLs; beyond this the oldest are released (a long scroll through the library). */
const kept = 150;

const urls = new Map<string, Promise<string | null>>();

/**
 * The app's photos: an `<img>` cannot send the bearer token, so each API photo is fetched (through the offline copies)
 * and shown from an object URL. Anything not under `/api/` (a picked file's preview) is shown as it is.
 */
export function startNativeImages(): void {
  useImageSource((url) => {
    if (!url.startsWith('/api/')) {
      return Promise.resolve(url);
    }

    let found = urls.get(url);

    if (!found) {
      found = fetchFile(url).then((blob) => (blob ? URL.createObjectURL(blob) : null));
      urls.set(url, found);
      release();
    } else {
      // Most recently used last, so a photo on screen is not the next one released.
      urls.delete(url);
      urls.set(url, found);
    }

    return found;
  });
}

/** Releases every photo, on sign-out: the next person must not be shown them. */
export function forgetNativeImages(): void {
  for (const url of urls.values()) {
    void url.then((objectUrl) => objectUrl && URL.revokeObjectURL(objectUrl));
  }

  urls.clear();
}

function release(): void {
  while (urls.size > kept) {
    const [oldest] = urls;

    if (!oldest) {
      return;
    }

    urls.delete(oldest[0]);
    void oldest[1].then((objectUrl) => objectUrl && URL.revokeObjectURL(objectUrl));
  }
}
