import { render } from '@testing-library/svelte';

import { defaultTheme } from '$ds/themes';

/**
 * Renders a component the way the app renders it.
 *
 * A component that reads a semantic token is unreadable without a theme on the
 * document, and a test that rendered it bare would pass while the real screen
 * was black on black. Everything a page gets from the shell is applied here
 * instead of being re-applied in every test.
 */
type Rendered = ReturnType<typeof render>;
type Component = Parameters<typeof render>[0];

export interface RenderOptions {
  readonly props?: Record<string, unknown>;
  readonly theme?: string;
  readonly mode?: 'light' | 'dark';
}

export function renderWithProviders(component: Component, options: RenderOptions = {}): Rendered {
  const root = document.documentElement;

  root.dataset['theme'] = options.theme ?? defaultTheme.id;
  root.dataset['mode'] = options.mode ?? 'light';

  return render(component, { props: options.props ?? {} });
}
