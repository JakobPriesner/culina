import { compile } from '@inlang/paraglide-js';

import { paraglideOptions } from './paraglide.ts';

// The dev server's structure, since this runs next to it.
await compile({
  ...paraglideOptions,
  outputStructure: 'locale-modules'
});
