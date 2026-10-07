import type { PreviewRecipe } from './recipes';

export type PreviewFilter = 'all' | 'favourites' | 'quick';

export const quickMinutes = 30;

export function createPreviewLibrary(recipes: readonly PreviewRecipe[]) {
  let search = $state('');
  let filter = $state<PreviewFilter>('all');
  let favourites = $state<string[]>(['orzo']);

  const matches = (recipe: PreviewRecipe) =>
    `${recipe.title} ${recipe.tag} ${recipe.ingredients.map((i) => i.name).join(' ')}`
      .toLocaleLowerCase()
      .includes(search.trim().toLocaleLowerCase());

  const shown = $derived(
    recipes.filter(
      (recipe) =>
        matches(recipe) &&
        (filter !== 'favourites' || favourites.includes(recipe.id)) &&
        (filter !== 'quick' || recipe.minutes <= quickMinutes)
    )
  );

  return {
    get search() {
      return search;
    },
    set search(value: string) {
      search = value;
    },
    get filter() {
      return filter;
    },
    set filter(value: PreviewFilter) {
      filter = value;
    },
    get shown() {
      return shown;
    },
    get narrowed() {
      return search !== '' || filter !== 'all';
    },

    isFavourite: (id: string) => favourites.includes(id),

    toggleFavourite(id: string) {
      favourites = favourites.includes(id)
        ? favourites.filter((value) => value !== id)
        : [...favourites, id];
    },

    reset() {
      search = '';
      filter = 'all';
    }
  };
}
