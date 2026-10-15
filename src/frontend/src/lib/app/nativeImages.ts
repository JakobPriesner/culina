import { fetchFile } from '$api';
import { useImageSource } from '$ds/display/imageSources';

/** Photos kept as object URLs; beyond this the oldest are released (a long scroll through the library). */
const kept = 150;

const urls = new Map<string, Promise<string | null>>();

/** How many images show each photo right now; a photo on screen is never released, however long the scroll. */
const showing = new Map<string, number>();

/**
 * The app's photos: an `<img>` cannot send the bearer token, so each API photo is fetched (through the offline copies)
 * and shown from an object URL. Anything not under `/api/` (a picked file's preview) is shown as it is.
 */
export function startNativeImages(): void {
  useImageSource(resolve, stopShowing);
}

function resolve(url: string): Promise<string | null> {
  if (!url.startsWith('/api/')) {
    return Promise.resolve(url);
  }

  showing.set(url, (showing.get(url) ?? 0) + 1);

  let found = urls.get(url);

  if (!found) {
    const asked = fetchFile(url).then((blob) => {
      if (blob) {
        return URL.createObjectURL(blob);
      }

      // Not kept: a failure (offline) must not be the answer until sign-out; the next ask fetches again.
      if (urls.get(url) === asked) {
        urls.delete(url);
      }

      return null;
    });

    found = asked;
    urls.set(url, found);
    release();
  } else {
    // Most recently used last, so a photo on screen is not the next one released.
    urls.delete(url);
    urls.set(url, found);
  }

  return found;
}

/** An image stopped showing the photo it got for this address. */
function stopShowing(url: string): void {
  const left = (showing.get(url) ?? 1) - 1;

  if (left > 0) {
    showing.set(url, left);
  } else {
    showing.delete(url);
    release();
  }
}

/** Releases every photo, on sign-out: the next person must not be shown them. */
export function forgetNativeImages(): void {
  for (const url of urls.values()) {
    void url.then((objectUrl) => objectUrl && URL.revokeObjectURL(objectUrl));
  }

  urls.clear();
  showing.clear();
}

/** Drops the oldest photos beyond the limit that nothing shows. */
function release(): void {
  for (const [address, photo] of urls) {
    if (urls.size <= kept) {
      return;
    }

    if (!showing.has(address)) {
      urls.delete(address);
      void photo.then((objectUrl) => objectUrl && URL.revokeObjectURL(objectUrl));
    }
  }
}
