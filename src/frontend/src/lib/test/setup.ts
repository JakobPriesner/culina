import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/svelte';
import { afterEach } from 'vitest';

// Components are unmounted between tests, so one test's dialog cannot be found
// by the next one's query.
afterEach(() => cleanup());
