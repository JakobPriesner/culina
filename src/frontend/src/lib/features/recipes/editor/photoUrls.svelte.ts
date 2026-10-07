import { onDestroy } from 'svelte';

/**
 * Previewable addresses for the photos a form holds.
 *
 * One object URL per photo for as long as the photo is held: adding one photo
 * does not re-read and re-decode the others, and a photo that is let go has its
 * URL revoked. Call it while a component initialises.
 */
export function createPhotoUrls(photos: () => readonly File[]) {
  const objectUrls = new WeakMap<File, string>();
  let withUrls: File[] = [];

  const urls = $derived(
    photos().map((file) => {
      let url = objectUrls.get(file);

      if (!url) {
        url = URL.createObjectURL(file);
        objectUrls.set(file, url);
        withUrls.push(file);
      }

      return url;
    })
  );

  const revoke = (kept: readonly File[]) => {
    for (const file of withUrls) {
      if (!kept.includes(file)) {
        URL.revokeObjectURL(objectUrls.get(file)!);
        objectUrls.delete(file);
      }
    }

    withUrls = withUrls.filter((file) => kept.includes(file));
  };

  $effect(() => revoke(photos()));
  onDestroy(() => revoke([]));

  return {
    get urls() {
      return urls;
    }
  };
}
