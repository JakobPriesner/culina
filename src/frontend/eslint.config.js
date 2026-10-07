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
      // The OpenAPI document is the contract: a cast around generated types means the backend is what to fix.
      '@typescript-eslint/no-explicit-any': 'error',
      '@typescript-eslint/consistent-type-imports': 'error',
      // A leading underscore marks a value discarded on purpose.
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
      // Computed `m[`key.${x}`]` lookups defeat message tree-shaking; write the keys out.
      'no-restricted-syntax': [
        'error',
        {
          selector:
            "MemberExpression[computed=true][object.name='m'][property.type='TemplateLiteral']",
          message:
            'Do not look up messages with a template literal: it defeats Paraglide tree-shaking. Map each key to its message explicitly.'
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
     * Route ids go through `resolve()` where written (`navigation.ts`, `redirectTarget.ts`); these
     * consumers use already-resolved values that the rule can't follow through a variable.
     */
    files: [
      'src/lib/app/Navigation.svelte',
      'src/routes/+layout.svelte',
      // Settings resolves its categories once into the list.
      'src/routes/(app)/me/+layout.svelte',
      // Auth pages and recipes link via `resolve()` plus a query string / yield in the URL, already resolved.
      'src/routes/(auth)/**',
      'src/routes/(app)/recipes/**',
      // A shared recipe reflects its yield into its own URL the same way.
      'src/routes/(public)/**'
    ],
    rules: { 'svelte/no-navigation-without-resolve': 'off' }
  },
  {
    // Design-system components take `href` as a prop and can't know route vs off-site;
    // the page resolves.
    files: ['src/lib/design-system/**'],
    rules: { 'svelte/no-navigation-without-resolve': 'off' }
  },
  {
    // The wrapper may call fetch and see the generated schema.
    files: ['src/lib/api/**'],
    rules: { 'no-restricted-globals': 'off', 'no-restricted-imports': 'off' }
  },
  {
    // The service worker has no window, session or store, so no typed client, and must never
    // touch the API (see its header).
    files: ['src/service-worker.ts', 'src/service-worker/**/*.ts'],
    rules: { 'no-restricted-globals': 'off' }
  },
  {
    ignores: [
      '.svelte-kit/',
      'build/',
      'node_modules/',
      // Generated from the OpenAPI document and messages/*.json; never hand-edited.
      'src/lib/api/generated/',
      'src/lib/paraglide/'
    ]
  }
);
