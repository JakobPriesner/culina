import type { Attachment } from 'svelte/attachments';

/**
 * Turns an image address into one an `<img>` can load. The website needs none: the browser fetches with the session
 * cookie. The installed app signs in with a bearer token an `<img>` cannot send, so it registers a source that fetches
 * the photo and hands back an object URL (see lib/app/nativeImages.ts). Null when the photo could not be read.
 */
export type ImageSource = (url: string) => Promise<string | null>;

/** Says an image no longer shows what an earlier call to the source answered, so that answer may be freed. Every call to the source is paired with one release, whatever its answer. */
export type ImageRelease = (url: string) => void;

let source: ImageSource | null = null;
let release: ImageRelease | null = null;

/** Registered once at start-up by a build that needs it. */
export function useImageSource(next: ImageSource | null, done: ImageRelease | null = null): void {
  source = next;
  release = done;
}

export function releaseImage(url: string): void {
  release?.(url);
}

export function imageSource(): ImageSource | null {
  return source;
}

/**
 * The `srcset` candidate a box `cssWidth` wide needs on this screen: the smallest at least as wide, else the widest.
 * What the browser does for `srcset`, done by hand when the address has to be fetched first.
 */
export function pickCandidate(
  src: string,
  srcset: string | undefined,
  cssWidth: number,
  density = globalThis.devicePixelRatio || 1
): string {
  const candidates = (srcset ?? '')
    .split(',')
    .map((entry) => entry.trim().split(/\s+/))
    .map(([url, descriptor]) => ({ url, width: Number.parseInt(descriptor ?? '', 10) }))
    .filter((candidate): candidate is { url: string; width: number } =>
      Boolean(candidate.url && Number.isFinite(candidate.width))
    )
    .sort((a, b) => a.width - b.width);

  if (candidates.length === 0 || cssWidth <= 0) {
    return src;
  }

  const needed = cssWidth * density;

  return (candidates.find((candidate) => candidate.width >= needed) ?? candidates.at(-1)!).url;
}

/** For a bare `<img>` with no `src` attribute: sets it, through the registered source if there is one. */
export function sourced(url: string): Attachment<HTMLImageElement> {
  return (image) => {
    let current = true;

    if (!source) {
      image.src = url;
    } else {
      void source(url).then((resolved) => {
        if (current && resolved) {
          image.src = resolved;
        }
      });
    }

    return () => {
      current = false;
    };
  };
}
