import js from '@eslint/js';
import svelte from 'eslint-plugin-svelte';
import globals from 'globals';
import ts from 'typescript-eslint';

export default ts.config(
  js.configs.recommended,
  ...ts.configs.recommended,
  ...svelte.configs.recommended,
  {
    languageOptions: {
      globals: { ...globals.browser, ...globals.node }
    }
  },
  {
    files: ['**/*.svelte', '**/*.svelte.ts'],
    languageOptions: {
      parserOptions: { parser: ts.parser }
    }
  },
  {
    rules: {
      // The API contract is the OpenAPI document. A cast around the generated
      // types means the contract is wrong, so the backend is what gets fixed.
      '@typescript-eslint/no-explicit-any': 'error',
      '@typescript-eslint/consistent-type-imports': 'error',
      // A leading underscore is how a value is discarded on purpose, which is
      // the only reason an unused binding should ever survive review.
      '@typescript-eslint/no-unused-vars': [
        'error',
        { argsIgnorePattern: '^_', varsIgnorePattern: '^_' }
      ],
      'no-restricted-globals': [
        'error',
        {
          name: 'fetch',
          message:
            'Use the typed client in $api/client. It owns credentials, CSRF, ETags and error mapping — see the frontend-api-client skill.'
        }
      ],
      'no-restricted-imports': [
        'error',
        {
          patterns: [
            {
              group: ['**/api/generated/*'],
              message:
                'Generated types are imported only by $api and feature mappers, never by components.'
            }
          ]
        }
      ]
    }
  },
  {
    /*
     * Every route id in Culina goes through `resolve()` where it is written —
     * `navigation.ts` and `redirectTarget.ts` — so a base path is honoured.
     * These consumers use values that are already resolved, and the
     * rule cannot see that through a variable. It stays on everywhere a literal
     * route could still be written by hand.
     */
    files: [
      'src/lib/app/Navigation.svelte',
      'src/lib/app/LibraryNav.svelte',
      'src/routes/+layout.svelte',
      // Settings lists its categories the way the shell lists its
      // destinations: resolved once into the list, rendered from it.
      'src/routes/(app)/me/+layout.svelte',
      // The auth pages link to each other through `resolve()` plus a query
      // string carrying where the person was going, and a recipe reflects its
      // chosen yield into its own URL — both already resolved, which the rule
      // cannot follow through a variable.
      'src/routes/(auth)/**',
      'src/routes/(app)/recipes/**'
    ],
    rules: { 'svelte/no-navigation-without-resolve': 'off' }
  },
  {
    // A design-system component takes its href as a prop: it cannot know
    // whether the caller is linking to a route or off site, so resolution stays
    // with the page that knows. The rule keeps working everywhere else, which
    // is where a literal internal link would actually appear.
    files: ['src/lib/design-system/**'],
    rules: { 'svelte/no-navigation-without-resolve': 'off' }
  },
  {
    // The wrapper is the one place allowed to call fetch and to see the
    // generated schema.
    files: ['src/lib/api/**'],
    rules: { 'no-restricted-globals': 'off', 'no-restricted-imports': 'off' }
  },
  {
    // The service worker runs with no window, no session and no store: the
    // typed client is not available to it, and the whole point of the file is
    // to answer requests the network cannot. It is also the one file that must
    // never touch the API — see the comment at the top of it.
    files: ['src/service-worker.ts'],
    rules: { 'no-restricted-globals': 'off' }
  },
  {
    ignores: [
      '.svelte-kit/',
      'build/',
      'node_modules/',
      // Both generated from a source of truth elsewhere: the backend's OpenAPI
      // document and messages/*.json. Never hand-edited, so never linted.
      'src/lib/api/generated/',
      'src/lib/paraglide/'
    ]
  }
);
