import { defaultTheme, isKnownTheme } from '$ds/themes';

/** What "how the app looks" means and how it is stored; browser-free so tests and the inline boot script's reference can read it. */

/** What a person chose. `system` is a choice, not the absence of one. */
export type Mode = 'light' | 'dark' | 'system';

export type ResolvedMode = 'light' | 'dark';

export interface Appearance {
  readonly theme: string;
  readonly mode: Mode;
}

/** The same key the inline script in `app.html` reads. Changing one changes both. */
export const storageKey = 'culina.appearance';

export const defaultAppearance: Appearance = { theme: defaultTheme.id, mode: 'system' };

const modes: readonly Mode[] = ['light', 'dark', 'system'];

const isMode = (value: unknown): value is Mode => modes.includes(value as Mode);

/** Reads a stored value without trusting it: hand edits and removed themes fall back rather than render unthemed. */
export function parseAppearance(raw: string | null): Appearance {
  if (!raw) {
    return defaultAppearance;
  }

  try {
    const stored: unknown = JSON.parse(raw);

    if (typeof stored !== 'object' || stored === null) {
      return defaultAppearance;
    }

    const { theme, mode } = stored as Partial<Appearance>;

    return {
      theme: isKnownTheme(theme) ? theme! : defaultAppearance.theme,
      mode: isMode(mode) ? mode : defaultAppearance.mode
    };
  } catch {
    return defaultAppearance;
  }
}

export function nextMode(mode: Mode): Mode {
  return modes[(modes.indexOf(mode) + 1) % modes.length]!;
}
