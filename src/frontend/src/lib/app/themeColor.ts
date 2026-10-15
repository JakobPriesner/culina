/**
 * The browser's colour around the page (the status bar, the tab strip) follows the mode chosen in the app, not only the
 * system's: app.html offers one `theme-color` per scheme by media query, and a chosen mode turns the other one off.
 * `null` is "as the system", which puts the media queries back.
 */
export function followMode(mode: 'light' | 'dark' | null, doc: Document = document): void {
  for (const meta of doc.querySelectorAll<HTMLMetaElement>('meta[name="theme-color"]')) {
    const scheme = (meta.dataset['scheme'] ??= /dark/.test(meta.getAttribute('media') ?? '')
      ? 'dark'
      : 'light');

    meta.setAttribute(
      'media',
      mode === null ? `(prefers-color-scheme: ${scheme})` : scheme === mode ? 'all' : 'not all'
    );
  }
}
