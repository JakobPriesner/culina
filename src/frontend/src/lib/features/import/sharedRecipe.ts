/** A device-local handoff from the share sheet, kept through sign-in and reload. */
export interface SharedRecipe {
  id: string;
  receivedAt: number;
  title: string;
  text: string;
  url: string;
  photos: File[];
}

const databaseName = 'culina-recipe-intake';
const maxAge = 24 * 60 * 60 * 1000;
export const sharedImageLimit = 10 * 1024 * 1024;
export const sharedTotalLimit = 40 * 1024 * 1024;
export const sharedPhotoLimit = 8;

function database(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    const opening = indexedDB.open(databaseName, 1);
    opening.onupgradeneeded = () => opening.result.createObjectStore('shares', { keyPath: 'id' });
    opening.onsuccess = () => resolve(opening.result);
    opening.onerror = () => reject(opening.error);
  });
}

async function stored<T>(
  mode: IDBTransactionMode,
  operation: (store: IDBObjectStore) => IDBRequest<T>
): Promise<T> {
  const db = await database();
  try {
    return await new Promise((resolve, reject) => {
      const transaction = db.transaction('shares', mode);
      const request = operation(transaction.objectStore('shares'));
      transaction.oncomplete = () => resolve(request.result);
      transaction.onerror = () => reject(transaction.error);
      transaction.onabort = () => reject(transaction.error);
    });
  } finally {
    db.close();
  }
}

export function sharedAddress(url: string, text: string): string {
  const candidate = url.trim() || text.match(/https?:\/\/[^\s<>]+/)?.[0] || '';
  try {
    const parsed = new URL(candidate);
    return parsed.protocol === 'http:' || parsed.protocol === 'https:' ? parsed.href : '';
  } catch {
    return '';
  }
}

export function validSharedPhotos(photos: readonly File[]): boolean {
  return (
    photos.length <= sharedPhotoLimit &&
    photos.every(
      (file) =>
        ['image/jpeg', 'image/png', 'image/webp'].includes(file.type) &&
        file.size > 0 &&
        file.size <= sharedImageLimit
    ) &&
    photos.reduce((total, file) => total + file.size, 0) <= sharedTotalLimit
  );
}

export async function keepSharedRecipe(form: FormData): Promise<string> {
  const photos = form
    .getAll('photos')
    .filter((value): value is File => typeof value !== 'string' && value.size > 0);
  if (!validSharedPhotos(photos)) throw new Error('unsupported-media');
  const field = (name: string) =>
    typeof form.get(name) === 'string' ? String(form.get(name)).slice(0, 20_000) : '';
  const shared: SharedRecipe = {
    id: crypto.randomUUID(),
    receivedAt: Date.now(),
    title: field('title'),
    text: field('text'),
    url: field('url'),
    photos
  };
  const previous = await stored<SharedRecipe[]>('readonly', (store) => store.getAll());
  // Bound abandoned handoffs, including ones from before sign-in.
  for (const item of previous.sort((a, b) => b.receivedAt - a.receivedAt).slice(3)) {
    await forgetSharedRecipe(item.id);
  }
  for (const item of previous.filter((item) => Date.now() - item.receivedAt > maxAge)) {
    await forgetSharedRecipe(item.id);
  }
  await stored('readwrite', (store) => store.put(shared));
  return shared.id;
}

export async function recallSharedRecipe(id: string): Promise<SharedRecipe | null> {
  const share = await stored<SharedRecipe | undefined>('readonly', (store) => store.get(id));
  if (!share) return null;
  if (Date.now() - share.receivedAt > maxAge) {
    await forgetSharedRecipe(id);
    return null;
  }
  return share;
}

export async function forgetSharedRecipe(id: string): Promise<void> {
  await stored('readwrite', (store) => store.delete(id));
}
