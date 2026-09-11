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
    // The wrapper is the one place allowed to call fetch and to see the
    // generated schema.
    files: ['src/lib/api/**'],
    rules: { 'no-restricted-globals': 'off', 'no-restricted-imports': 'off' }
  },
  {
    ignores: ['.svelte-kit/', 'build/', 'node_modules/', 'src/lib/api/generated/']
  }
);
