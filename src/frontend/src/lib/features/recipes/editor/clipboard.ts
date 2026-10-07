export const canReadClipboard =
  typeof navigator !== 'undefined' &&
  'clipboard' in navigator &&
  typeof navigator.clipboard?.readText === 'function';

/** The clipboard text and its first web address; null when unreadable or refused (manual paste remains). */
export async function readClipboardRecipe(): Promise<{ text: string; url?: string } | null> {
  if (!canReadClipboard) {
    return null;
  }

  try {
    const text = (await navigator.clipboard.readText()).trim();
    const url = text.match(/https?:\/\/[^\s]+/)?.[0];

    return url ? { text, url } : { text };
  } catch {
    return null;
  }
}
