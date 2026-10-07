import { paraglideVitePlugin } from '@inlang/paraglide-js';

import { galleryOnly } from './build-tools/gallery.js';
import { paraglideOptions } from './build-tools/paraglide.js';
import { siteFiles } from './build-tools/siteFiles.js';
import adapter from '@sveltejs/adapter-static';
import { sveltekit } from '@sveltejs/kit/vite';
import { defineConfig } from 'vitest/config';

// Under Vitest Svelte must resolve to its client build, or every render fails with "mount(...) is not available on the server".
const underTest = Boolean(process.env['VITEST']);

const galleryWanted = process.env['VITE_GALLERY'] === '1';

const apiProxy = {
  '/api': {
    target: process.env['CULINA_API'] ?? 'http://localhost:5000',
    changeOrigin: false
  }
};

function messages(outputStructure: 'message-modules' | 'locale-modules') {
  return paraglideVitePlugin({
    ...paraglideOptions,
    outputStructure
  });
}

export default defineConfig({
  // A literal so the bundler can fold it: the gallery must vanish from release builds (see src/lib/app/gallery.ts).
  define: {
    __CULINA_GALLERY__: JSON.stringify(galleryWanted)
  },

  plugins: [
    // First: replaces the gallery's components with empty ones in release builds.
    galleryOnly(galleryWanted),

    // Tree-shakeable message functions. The dev server gets one module per locale because per-message modules made page loads ~850ms.
    // `pnpm messages` writes the dev layout too, so checks beside a dev server do not restore the slow one.
    { ...messages('message-modules'), apply: 'build' },
    { ...messages('locale-modules'), apply: 'serve' },

    sveltekit({
      compilerOptions: {
        // Runes everywhere in our own code; libraries keep their own mode.
        runes: ({ filename }) =>
          filename.split(/[/\\]/).includes('node_modules') ? undefined : true
      },

      // A static SPA served by the .NET host from the API's origin; `fallback` serves index.html for client routes.
      adapter: adapter({ fallback: 'index.html', strict: false }),

      // Absolute asset URLs: a document built for `/` but served for `/recipes/<id>` (service worker shell, host deep links)
      // would resolve `./_app/…` against `/recipes/` and boot blank.
      paths: { relative: false },

      // Registered by the app so it can ask before swapping a build mid-recipe (see lib/app/updates).
      serviceWorker: { register: false },

      alias: {
        $api: 'src/lib/api',
        $ds: 'src/lib/design-system',
        $features: 'src/lib/features',
        $shell: 'src/lib/app'
      }
    }),

    // robots.txt and sitemap.xml from the one public-route list; last, so it writes into the finished adapter output.
    siteFiles()
  ],

  resolve: underTest ? { conditions: ['browser'] } : {},

  // Same-origin dev (proxied /api) so cookies, SameSite and CSRF behave as in production; hence no CORS or dev-only auth.
  server: { host: '0.0.0.0', port: 5173, proxy: apiProxy },

  // Same proxy for `vite preview`, which the e2e suite runs against a real backend.
  preview: { proxy: apiProxy },

  test: {
    // jsdom: anything needing real layout or a service worker is a Playwright test.
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/lib/test/setup.ts'],
    include: ['src/**/*.{test,spec}.{js,ts}', 'build-tools/**/*.spec.ts'],
    css: true
  }
});
