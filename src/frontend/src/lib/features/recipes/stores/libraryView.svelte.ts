import { registerStore } from '$shell/stores';

/** The app's own sort names; each is an order `GET /recipes` accepts, and `toWireSort` is where they meet the query string. */
export type RecipeSort =
  | 'relevance'
  | 'suggested'
  | 'recent'
  | 'title'
  | 'quickest'
  | 'mostCooked'
  /** A cookbook's own order; only meaningful inside one. */
  | 'shelf';

export interface SortContext {
  readonly searching: boolean;
  /** Whether the ranking has anything to say about this kitchen yet. */
  readonly ranks: boolean;
  readonly inACookbook: boolean;
}

/** The order a list is actually in, shared by the request and its label so the two cannot disagree. */
export function effectiveSort(chosen: RecipeSort | null, context: SortContext): RecipeSort {
  if (chosen !== null) {
    return chosen;
  }

  if (context.searching) {
    return 'relevance';
  }

  if (context.inACookbook) {
    return 'shelf';
  }

  return 'recent';
}

export function sortsFor(context: SortContext): readonly RecipeSort[] {
  return [
    // "Best match" of nothing is not an order anybody means.
    ...(context.searching ? (['relevance'] as const) : []),
    ...(context.ranks ? (['suggested'] as const) : []),
    ...(context.inACookbook ? (['shelf'] as const) : []),
    'recent',
    'title',
    'quickest',
    'mostCooked'
  ];
}

export function toWireSort(sort: RecipeSort): string {
  switch (sort) {
    case 'relevance':
      return 'relevance';
    case 'suggested':
      return 'suggested';
    case 'title':
      return 'title';
    case 'quickest':
      return 'totalMinutes';
    case 'mostCooked':
      return '-cookCount';
    case 'shelf':
      return 'cookbookOrder';
    default:
      return '-updatedAt';
  }
}

export function fromWireSort(wire: string | null | undefined): RecipeSort | null {
  switch (wire) {
    case 'relevance':
      return 'relevance';
    case 'suggested':
      return 'suggested';
    case 'title':
      return 'title';
    case 'totalMinutes':
      return 'quickest';
    case '-cookCount':
      return 'mostCooked';
    case '-updatedAt':
      return 'recent';
    default:
      // A saved search may hold `cookbookOrder`, which needs a cookbook the library lacks.
      return null;
  }
}

/** Time ceiling buckets: "about half an hour" is the thought, not 37 minutes. */
export const timeCeilings = [15, 30, 45, 60] as const;

/** One object for the question the page, cookbook page, filter panel and saved searches all ask, so they cannot diverge. */
export class RecipeQuery {
  /** Applied words, not what is half-typed. */
  query = $state('');

  tags = $state<readonly string[]>([]);

  maxMinutes = $state<number | null>(null);

  /** The chosen order; null means the page decides (`effectiveSort`), so a pick sticks even when ranking would take over. */
  sort = $state<RecipeSort | null>(null);

  /** Filters on, for the trigger badge; words are excluded as they are visible in the box. */
  get activeCount(): number {
    return this.tags.length + (this.maxMinutes === null ? 0 : 1) + (this.sort === null ? 0 : 1);
  }

  get filtered(): boolean {
    return this.query.trim().length > 0 || this.tags.length > 0 || this.maxMinutes !== null;
  }

  toggleTag(slug: string): void {
    this.tags = this.tags.includes(slug)
      ? this.tags.filter((one) => one !== slug)
      : [...this.tags, slug];
  }

  assign(next: {
    query?: string;
    tags?: readonly string[];
    maxMinutes?: number | null;
    sort?: RecipeSort | null;
  }): void {
    this.query = next.query ?? '';
    this.tags = next.tags ?? [];
    this.maxMinutes = next.maxMinutes ?? null;
    this.sort = next.sort ?? null;
  }

  snapshot(): {
    query: string;
    tags: readonly string[];
    maxMinutes: number | null;
    sort: RecipeSort | null;
  } {
    return {
      query: this.query,
      tags: this.tags,
      maxMinutes: this.maxMinutes,
      sort: this.sort
    };
  }

  clear(): void {
    this.query = '';
    this.tags = [];
    this.maxMinutes = null;
    this.sort = null;
  }
}

/** The library's own query, kept through a recipe round trip and scoped to the household (another kitchen's tag slugs match nothing). */
class LibraryView extends RecipeQuery {
  #householdId: string | null = null;

  forHousehold(id: string): void {
    if (this.#householdId !== id) {
      this.reset();
      this.#householdId = id;
    }
  }

  reset(): void {
    this.clear();
    this.#householdId = null;
  }
}

export const libraryView = new LibraryView();
registerStore(() => libraryView.reset());
