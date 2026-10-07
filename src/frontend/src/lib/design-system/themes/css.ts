/** A tiny CSS reader for the theme contract test: it only finds custom-property declarations in top-level blocks. */
export interface CssBlock {
  readonly selector: string;
  readonly declarations: ReadonlyMap<string, string>;
}

export function readBlocks(css: string): CssBlock[] {
  const withoutComments = css.replace(/\/\*[\s\S]*?\*\//g, '');
  const blocks: CssBlock[] = [];
  const pattern = /([^{}]+)\{([^{}]*)\}/g;

  let match: RegExpExecArray | null;

  while ((match = pattern.exec(withoutComments)) !== null) {
    const selector = match[1]!.trim();
    const declarations = new Map<string, string>();

    for (const declaration of match[2]!.split(';')) {
      const [name, ...rest] = declaration.split(':');

      if (name && rest.length > 0 && name.trim().startsWith('--')) {
        declarations.set(name.trim(), rest.join(':').trim());
      }
    }

    blocks.push({ selector, declarations });
  }

  return blocks;
}

export function declaredProperties(css: string): Set<string> {
  const names = new Set<string>();

  for (const block of readBlocks(css)) {
    for (const name of block.declarations.keys()) {
      names.add(name);
    }
  }

  return names;
}
