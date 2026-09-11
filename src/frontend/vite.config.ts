import adapter from '@sveltejs/adapter-static';
import { sveltekit } from '@sveltejs/kit/vite';
import { defineConfig } from 'vite';

export default defineConfig({
  plugins: [
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

      alias: {
        $api: 'src/lib/api',
        $ds: 'src/lib/design-system',
        $features: 'src/lib/features',
        $shell: 'src/lib/app'
      }
    })
  ],

  server: {
    port: 5173,
    proxy: {
      // Development is same-origin on purpose. Proxying /api means cookies,
      // SameSite and CSRF behave exactly as they do in production, which is why
      // Culina has no CORS policy anywhere and no dev-only auth path.
      '/api': {
        target: 'http://localhost:5000',
        changeOrigin: false
      }
    }
  }
});
