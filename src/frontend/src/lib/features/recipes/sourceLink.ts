/** A link to where a recipe came from: where it goes, and what it is called. */
export interface SourceLink {
  readonly href: string;
  /** The host, without a leading `www.`: "chefkoch.de" is the fact worth showing. */
  readonly host: string;
}

/**
 * The original's address as a link, or nothing: re-checked where it becomes an `href` (absolute http(s) with a host; no `javascript:`, `data:` or app schemes).
 * The label is read from the same parsed address, so `javascript://chefkoch.de/…` never shows as "from chefkoch.de".
 */
export function sourceLink(address: string | null | undefined): SourceLink | null {
  if (!address) {
    return null;
  }

  let url: URL;

  try {
    url = new URL(address);
  } catch {
    return null;
  }

  if ((url.protocol !== 'http:' && url.protocol !== 'https:') || !url.hostname) {
    return null;
  }

  return { href: url.href, host: url.host.replace(/^www\./, '') };
}
