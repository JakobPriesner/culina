import { tick } from 'svelte';

/** The nutrition headline as the meta line shows it: short, and a way to open the full panel. */
export interface NutritionLink {
  readonly label: string;
  /** Names the button in full, starting with what it shows. */
  readonly ariaLabel: string;
  readonly onopen: () => void;
}
/** The shared and household pages open the same disclosure, including focus and reduced motion. */
export async function revealNutritionPanel(): Promise<void> {
  await tick();

  const panel = document.getElementById('nutrition');
  const calm = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  panel?.scrollIntoView({ behavior: calm ? 'auto' : 'smooth', block: 'start' });
  panel?.querySelector('summary')?.focus({ preventScroll: true });
}
