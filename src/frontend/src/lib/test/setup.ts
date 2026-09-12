import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/svelte';
import { afterEach } from 'vitest';

import { resetAll } from './resettable';

// Components are unmounted and module-level state is restored between tests, so
// one test's dialog cannot be found by the next one's query and one test's
// signed-in user cannot leak into the next one's assertions.
afterEach(() => {
  cleanup();
  resetAll();
  document.documentElement.removeAttribute('data-theme');
  document.documentElement.removeAttribute('data-mode');
});
