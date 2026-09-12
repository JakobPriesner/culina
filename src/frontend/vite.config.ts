import { paraglideVitePlugin } from '@inlang/paraglide-js';

import { galleryOnly } from './build-tools/gallery.js';
import { siteFiles } from './build-tools/siteFiles.js';
import adapter from '@sveltejs/adapter-static';
import { sveltekit } from '@sveltejs/kit/vite';
import { defineConfig } from 'vitest/config';

// Under Vitest, Svelte must resolve to its client build: without this the
// component entry points resolve to the server build and every render fails
// with "mount(...) is not available on the server".
const underTest = Boolean(process.env['VITEST']);

// The gallery is built in only when the end-to-end runner asks for it.
const galleryWanted = process.env['VITE_GALLERY'] === '1';

const apiProxy = {
  '/api': {
    target: process.env['CULINA_API'] ?? 'http://localhost:5000',
    changeOrigin: false
  }
};

export default defineConfig({
  // A literal, always. The gallery has to disappear from a release build, and
  // it can only disappear if the condition guarding it is a constant the
  // bundler can fold — which `import.meta.env.VITE_GALLERY` is not when the
  // variable is unset. See src/lib/app/gallery.ts.
  define: {
    __CULINA_GALLERY__: JSON.stringify(galleryWanted)
  },

  plugins: [
    // Before everything: it replaces the gallery's components with empty ones
    // in a release build, so no specimen markup is ever compiled.
    galleryOnly(galleryWanted),

    // Messages compile to tree-shakeable functions, so there is no runtime
    // dictionary to ship and a key that does not exist is a compile error
    // rather than an empty string in production.
    paraglideVitePlugin({
      project: './project.inlang',
      outdir: './src/lib/paraglide',
      emitTsDeclarations: true,
      // The locale a signed-in person chose is applied by the preferences
      // store once the session is known. Before that — and for a visitor who
      // has never signed in — the last choice on this device wins, then the
      // browser's own language, then English.
      strategy: ['localStorage', 'preferredLanguage', 'baseLocale'],
      localStorageKey: 'culina.locale'
    }),

    sveltekit({
      compilerOptions: {
        // Runes everywhere in our own code; libraries keep their own mode.
        runes: ({ filename }) =>
          filename.split(/[/\\]/).includes('node_modules') ? undefined : true
      },

      // Culina ships as a static SPA served by the .NET host from the same
      // origin as the API. `fallback` makes the host serve index.html for
      // every client route.
      adapter: adapter({ fallback: 'index.html', strict: false }),

      // Absolute asset URLs, not relative ones.
      //
      // Culina is always served from the root of its own origin, so relative
      // paths buy nothing — and they cost: a document generated for `/` and
      // served for `/recipes/<id>` resolves `./_app/…` against `/recipes/`,
      // and the app boots to a blank page. That is exactly what the service
      // worker does with the shell, and it is what the host does for a deep
      // link. Absolute paths make every document interchangeable.
      paths: { relative: false },

      // Registered by the app, not by the framework: Culina asks before it
      // swaps a running build out from under someone mid-recipe, and that
      // conversation needs the registration object. See lib/app/updates.
      serviceWorker: { register: false },

      alias: {
        $api: 'src/lib/api',
        $ds: 'src/lib/design-system',
        $features: 'src/lib/features',
        $shell: 'src/lib/app'
      }
    }),

    // robots.txt, sitemap.xml and security.txt, written from the one list of
    // public routes so they cannot disagree with each other. Last, so it writes
    // into the directory the adapter has finished producing.
    siteFiles()
  ],

  resolve: underTest ? { conditions: ['browser'] } : {},

  // Development is same-origin on purpose. Proxying /api means cookies,
  // SameSite and CSRF behave exactly as they do in production, which is why
  // Culina has no CORS policy anywhere and no dev-only auth path.
  server: { port: 5173, proxy: apiProxy },

  // The same proxy for `vite preview`, so the end-to-end suite exercises the
  // built app against a real backend rather than a different arrangement.
  preview: { proxy: apiProxy },

  test: {
    // jsdom, not a real browser: these suites cover tokens, stores and
    // component behaviour. Anything that needs a real layout or a real service
    // worker is a Playwright test instead.
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/lib/test/setup.ts'],
    include: ['src/**/*.{test,spec}.{js,ts}', 'build-tools/**/*.spec.ts'],
    css: true
  }
});
