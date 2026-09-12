/**
 * The themes this build ships.
 *
 * Adding one is a CSS file next to this and one entry here. Components never
 * learn about it: they only ever name semantic tokens, which every theme is
 * required to define.
 */
export interface Theme {
  /** The value of `data-theme`, and the id stored in a user's settings. */
  readonly id: string;
  /** What the theme is called in the settings screen. */
  readonly label: string;
}

export const themes: readonly Theme[] = [{ id: 'warm-paper', label: 'Warm paper' }];

/** The theme a new account starts with, and the fallback for an unknown id. */
export const defaultTheme = themes[0]!;

/** Whether an id names a theme this build ships. */
export function isKnownTheme(id: string | null | undefined): boolean {
  return themes.some((theme) => theme.id === id);
}
