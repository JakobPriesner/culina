/** Keep the companion in the same design system, including lazy-loaded CSS. */
export function mirrorPipDocument(source: Document, target: Document): () => void {
  // Culina forbids base elements in CSP; resources retain absolute URLs.
  let copied: Element[] = [];

  const appearance = () => {
    const from = source.documentElement;
    const to = target.documentElement;
    const names = new Set([...from.attributes, ...to.attributes].map((attr) => attr.name));
    for (const name of names) {
      if (!['lang', 'dir', 'class'].includes(name) && !name.startsWith('data-')) continue;
      const value = from.getAttribute(name);
      if (value === null) to.removeAttribute(name);
      else to.setAttribute(name, value);
    }
    // CSSOM writes remain compatible with the inherited CSP; copying a style
    // attribute would turn runtime layout values into blocked inline markup.
    for (const name of Array.from(to.style)) {
      if (!from.style.getPropertyValue(name)) to.style.removeProperty(name);
    }
    for (const name of Array.from(from.style)) {
      to.style.setProperty(
        name,
        from.style.getPropertyValue(name),
        from.style.getPropertyPriority(name)
      );
    }
  };

  const styles = () => {
    const next: Element[] = [];
    const nonce = (source.querySelector('style[nonce], script[nonce]') as HTMLElement | null)
      ?.nonce;
    for (const sheet of source.styleSheets) {
      if (sheet.disabled) continue;
      try {
        const style = target.createElement('style');
        if (nonce) style.nonce = nonce;
        const css = [...sheet.cssRules].map((rule) => rule.cssText).join('\n');
        // Font/image URLs inside a linked sheet are relative to that sheet.
        // The child is about:blank and Culina's CSP forbids adding a base tag.
        const base = sheet.href ?? source.baseURI;
        style.textContent = css.replace(
          /url\(\s*(?:"([^"]*)"|'([^']*)'|([^)]*))\s*\)/gi,
          (
            match,
            double: string | undefined,
            single: string | undefined,
            bare: string | undefined
          ) => {
            const url = (double ?? single ?? bare ?? '').trim();
            if (!url || url.startsWith('#') || /^(data|blob):/i.test(url)) return match;
            try {
              return `url(${JSON.stringify(new URL(url, base).href)})`;
            } catch {
              return match;
            }
          }
        );
        style.media = sheet.media?.mediaText ?? '';
        next.push(style);
      } catch {
        // Cross-origin CSS can be loaded but cannot be read through cssRules.
        if (sheet.href) {
          const link = target.createElement('link');
          link.rel = 'stylesheet';
          link.href = sheet.href;
          link.media = sheet.media?.mediaText ?? '';
          next.push(link);
        }
      }
    }
    for (const link of source.head.querySelectorAll('link[as="font"]')) {
      if (
        next.some(
          (node) =>
            node.tagName === 'LINK' &&
            (node as HTMLLinkElement).href === (link as HTMLLinkElement).href
        )
      )
        continue;
      const clone = link.cloneNode(true) as HTMLLinkElement;
      clone.href = (link as HTMLLinkElement).href;
      next.push(clone);
    }
    target.head.append(...next);
    for (const node of copied) node.remove();
    copied = next;
  };

  appearance();
  styles();
  const rootObserver = new MutationObserver(appearance);
  rootObserver.observe(source.documentElement, { attributes: true });
  const headObserver = new MutationObserver(styles);
  headObserver.observe(source.head, {
    childList: true,
    subtree: true,
    characterData: true,
    attributes: true
  });
  return () => {
    rootObserver.disconnect();
    headObserver.disconnect();
    copied.forEach((node) => node.remove());
  };
}
