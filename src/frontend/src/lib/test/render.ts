import { render } from '@testing-library/svelte';

import { defaultTheme } from '$ds/themes';

/**
 * Renders a component with the shell's theme applied: a semantic-token component is unreadable bare,
 * and a bare test would pass on a black-on-black screen.
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
