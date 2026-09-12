// See https://svelte.dev/docs/kit/types#app.d.ts
// for information about these interfaces
declare global {
  /**
   * Whether the design-system gallery is built in.
   *
   * Replaced by Vite's `define` with a literal `true` or `false`, so a release
   * build can have the whole gallery removed by the bundler. An
   * `import.meta.env.VITE_*` read cannot do that job: Vite substitutes those
   * only when the variable has a value, and the release case is precisely the
   * one where it does not — so the expression survives as a runtime read and
   * every specimen ships. See src/lib/app/gallery.ts.
   */
  const __CULINA_GALLERY__: boolean;

  namespace App {
    // interface Error {}
    // interface Locals {}
    // interface PageData {}
    // interface PageState {}
    // interface Platform {}
  }
}

export {};
