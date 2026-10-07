/**
 * The themes this build ships; adding one is a CSS file and an entry here, as components only name
 * semantic tokens.
 */
export interface Theme {
  readonly id: string;
  readonly label: string;
}

export const themes: readonly Theme[] = [{ id: 'warm-paper', label: 'Warm paper' }];

export const defaultTheme = themes[0]!;

export function isKnownTheme(id: string | null | undefined): boolean {
  return themes.some((theme) => theme.id === id);
}
