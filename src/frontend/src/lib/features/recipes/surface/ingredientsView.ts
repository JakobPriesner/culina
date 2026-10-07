/**
 * How this person reads an ingredient list (`combined` or `perStep`); kept on the device, not in
 * the URL, so a shared link arrives arranged the way the reader reads.
 */
export type IngredientsView = 'combined' | 'perStep';

const key = 'culina.ingredientsView';

export function recallIngredientsView(): IngredientsView {
  try {
    return localStorage.getItem(key) === 'perStep' ? 'perStep' : 'combined';
  } catch {
    return 'combined';
  }
}

export function rememberIngredientsView(view: IngredientsView): void {
  try {
    localStorage.setItem(key, view);
  } catch {
    // Applied, but will not survive a reload.
  }
}
