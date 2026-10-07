export const canReadClipboard =
  typeof navigator !== 'undefined' &&
  'clipboard' in navigator &&
  typeof navigator.clipboard?.readText === 'function';

/**
 * What was copied, and the first web address in it, if there is one.
 *
 * Null when the clipboard cannot be read or reading it was refused: pasting by
 * hand is still there, so neither is worth an error.
 */
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
