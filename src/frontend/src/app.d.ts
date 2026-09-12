// See https://svelte.dev/docs/kit/types#app.d.ts
// for information about these interfaces
declare global {
  /**
   * Build-time flags, declared so they can be read with dot notation.
   *
   * The spelling matters: Vite replaces `import.meta.env.NAME` in the source
   * text, and only that form. A bracket lookup survives into the bundle as a
   * runtime read, and nothing that depends on it can be removed by the bundler.
   */
  interface ImportMetaEnv {
    /** `'1'` builds the design-system gallery in. Set only by the e2e runner. */
    readonly VITE_GALLERY?: string;
  }

  namespace App {
    // interface Error {}
    // interface Locals {}
    // interface PageData {}
    // interface PageState {}
    // interface Platform {}
  }
}

export {};
