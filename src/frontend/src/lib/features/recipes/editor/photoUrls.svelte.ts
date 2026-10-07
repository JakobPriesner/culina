import { onDestroy } from 'svelte';

/**
 * One object URL per held photo, so adding one does not re-decode the rest; a released photo's URL is revoked.
 * Call while a component initialises.
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
