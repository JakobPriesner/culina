/** A link to where a recipe came from: where it goes, and what it is called. */
export interface SourceLink {
  readonly href: string;
  /** The host, without a leading `www.`: "chefkoch.de" is the fact worth showing. */
  readonly host: string;
}

/**
 * The original's address as a link, or nothing.
 *
 * The address comes from places this app does not control — a connected
 * Tandoor, a page's redirect, a share sheet — and the server checks it, but a
 * link is only as safe as the last thing that looked at it. So it is checked
 * again here, where it becomes an `href`: an absolute `http` or `https` address
 * with a host, or no link at all. `javascript:`, `data:` and the schemes desktop
 * apps register for themselves are not places a recipe should send anyone.
 *
 * The label is read from the same parsed address the link points at, so
 * `javascript://chefkoch.de/…` can never be shown as "from chefkoch.de".
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
