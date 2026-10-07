/** What another app handed this one through the share target's query string. */
export interface IncomingShare {
  title: string;
  /** An address to import from, or '' when there was none. */
  url: string;
  text: string;
}

const isAddress = (value: string) => value.startsWith('http://') || value.startsWith('https://');

/**
 * Reads what was shared to the new-recipe page.
 *
 * A share sheet is loose about where it puts the address: some apps send it as
 * `url`, others leave it inside the `text` they sent along with it.
 */
export function readIncomingShare(params: URLSearchParams): IncomingShare {
  const url = params.get('url')?.trim() ?? '';
  const text = params.get('text')?.trim() ?? '';

  return {
    title: params.get('title')?.trim() ?? '',
    url: isAddress(url) ? url : (text.match(/https?:\/\/[^\s]+/)?.[0] ?? ''),
    text
  };
}
