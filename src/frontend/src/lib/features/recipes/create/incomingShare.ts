export interface IncomingShare {
  title: string;
  /** Address to import from, or ''. */
  url: string;
  text: string;
}

const isAddress = (value: string) => value.startsWith('http://') || value.startsWith('https://');

/** Reads a share-target query; apps put the address in `url` or inside `text`. */
export function readIncomingShare(params: URLSearchParams): IncomingShare {
  const url = params.get('url')?.trim() ?? '';
  const text = params.get('text')?.trim() ?? '';

  return {
    title: params.get('title')?.trim() ?? '',
    url: isAddress(url) ? url : (text.match(/https?:\/\/[^\s]+/)?.[0] ?? ''),
    text
  };
}
