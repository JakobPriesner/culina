/** The nutrition headline as the meta line shows it: short, and a way to open the full panel. */
export interface NutritionLink {
  readonly label: string;
  /** Names the button in full, starting with what it shows. */
  readonly ariaLabel: string;
  readonly onopen: () => void;
}
