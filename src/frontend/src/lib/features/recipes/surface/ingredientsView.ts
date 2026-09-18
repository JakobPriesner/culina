/**
 * How this person reads an ingredient list.
 *
 * `combined` is the whole list in one place, added up — what you read before
 * you shop, and what you check before you start. `perStep` puts each step's
 * ingredients beside the step, which is what you want once you are cooking from
 * the page rather than deciding whether to.
 *
 * Remembered on the device rather than in the URL: the yield is part of what a
 * recipe link means and this is not — a link sent to somebody should arrive
 * arranged the way *they* read, not the way the sender does.
 */
export type IngredientsView = 'combined' | 'perStep';

const key = 'culina.ingredientsView';

/** Never throws: not remembering is a worse page, not a broken one. */
export function recallIngredientsView(): IngredientsView {
  try {
    return localStorage.getItem(key) === 'perStep' ? 'perStep' : 'combined';
  } catch {
    // Private browsing, or a browser set to block site data.
    return 'combined';
  }
}

export function rememberIngredientsView(view: IngredientsView): void {
  try {
    localStorage.setItem(key, view);
  } catch {
    // The choice is applied; it just will not survive a reload.
  }
}
