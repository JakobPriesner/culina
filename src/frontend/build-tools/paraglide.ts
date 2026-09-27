import type { CompilerOptions } from '@inlang/paraglide-js';

/**
 * How the message catalogues are compiled, for the Vite plugin and for
 * `pnpm messages` alike.
 *
 * Both write the same `src/lib/paraglide`, so a CLI that compiled with
 * Paraglide's default strategy overwrote the dev server's runtime every time
 * `pnpm check` or `pnpm test:unit` ran beside it, and the locale picker quietly
 * behaved differently until the dev server restarted.
 */
export const paraglideOptions = {
  project: './project.inlang',
  outdir: './src/lib/paraglide',
  emitTsDeclarations: true,
  // The locale a signed-in person chose is applied by the preferences
  // store once the session is known. Before that — and for a visitor who
  // has never signed in — the last choice on this device wins, then the
  // browser's own language, then English. `custom-choice` is defined in
  // src/lib/app/i18n.ts; it reads nothing for `system`, which is how the
  // device's language gets through.
  strategy: ['custom-choice', 'preferredLanguage', 'baseLocale']
} satisfies CompilerOptions;
