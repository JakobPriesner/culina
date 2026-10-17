import { readDevice, writeDevice } from '$shell/deviceStorage';

/**
 * How a phone lists recipes: `rows` (thumbnail beside the title, several to a screen, for finding one) or
 * `photos` (one large photo each, for browsing). Wider screens show their cards either way. Kept on the
 * device, as how one reads a list depends on the screen, not the account.
 */
export type RecipeLayout = 'rows' | 'photos';

const key = 'culina.recipeLayout';

class RecipeLayoutChoice {
  current = $state<RecipeLayout>(readDevice(key) === 'photos' ? 'photos' : 'rows');

  choose(layout: RecipeLayout): void {
    this.current = layout;
    writeDevice(key, layout);
  }
}

export const recipeLayout = new RecipeLayoutChoice();
