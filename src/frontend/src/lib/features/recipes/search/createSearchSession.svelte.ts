import { m } from '$shell/i18n';

import type { createRecipeStore } from '../stores/recipes.svelte';
import type { Completion, RecipeSummary, SearchChip } from '../types';
import type { createCompletionStore } from './stores/completions.svelte';
import { withoutChip } from './wording';

/** One row of the listbox the arrow keys walk through. */
export type SearchOption =
  | {
      readonly key: string;
      readonly index: number;
      readonly kind: 'completion';
      readonly completion: Completion;
    }
  | {
      readonly key: string;
      readonly index: number;
      readonly kind: 'result';
      readonly recipe: RecipeSummary;
    };

interface Deps {
  readonly recipes: ReturnType<typeof createRecipeStore>;
  readonly completions: ReturnType<typeof createCompletionStore>;
  readonly householdId: () => string;
  /** Hands the typing back to the field. */
  readonly focus: () => void;
  /** Opens a recipe, in this tab or beside it. */
  readonly openRecipe: (recipeId: string, elsewhere: boolean) => void;
}

/**
 * What is in the search box, what was searched for, and what the keys do
 * with it.
 *
 * Everything the overlay reads from the field is a consequence of `typed`,
 * the tag filters and the server's answer; this is where they change together.
 */
export function createSearchSession({
  recipes,
  completions,
  householdId,
  focus,
  openRecipe
}: Deps) {
  /** What is in the box. */
  let typed = $state('');
  /** What was last searched for, which the chips' spans refer to. */
  let applied = $state('');
  /** Tag filters picked from a completion or a refinement, by slug. */
  let tags = $state<{ slug: string; name: string }[]>([]);
  /** The query whose correction the reader turned down. */
  let asTypedFor = $state<string | null>(null);
  let highlighted = $state(-1);

  let suggesting: ReturnType<typeof setTimeout> | undefined;
  let searching: ReturnType<typeof setTimeout> | undefined;

  const asking = $derived(applied.trim().length > 0 || tags.length > 0);

  /** Everything the arrow keys walk through, in the order it is drawn. */
  const options = $derived.by((): SearchOption[] => {
    const shown = typed.trim().length > 0 ? completions.items : [];
    const found = asking ? recipes.items : [];

    return [
      ...shown.map((completion, index): SearchOption => ({
        key: `c${index}`,
        index,
        kind: 'completion',
        completion
      })),
      ...found.map((recipe, index): SearchOption => ({
        key: `r${recipe.id}`,
        index: shown.length + index,
        kind: 'result',
        recipe
      }))
    ];
  });

  function cancelPending() {
    clearTimeout(suggesting);
    clearTimeout(searching);
  }

  /**
   * Two debounces, because the two answers cost different amounts: a
   * completion is a prefix over a few small tables and can keep up with the
   * word, the results are four lanes and arrive as a thought finishes.
   */
  function type(value: string) {
    typed = value;
    highlighted = -1;
    cancelPending();
    suggesting = setTimeout(() => void completions.complete(householdId(), value), 120);
    searching = setTimeout(() => search(value), 200);
  }

  function search(value: string) {
    applied = value;

    if (value.trim().length > 0 || tags.length > 0) {
      void recipes.list(householdId(), {
        query: value,
        tags: tags.map((tag) => tag.slug),
        asTyped: asTypedFor !== null && asTypedFor === value
      });
    }
  }

  /** Puts something in the box and searches it now, as if it had been typed and waited for. */
  function set(value: string) {
    typed = value;
    highlighted = -1;
    cancelPending();
    void completions.complete(householdId(), value);
    search(value);
    focus();
  }

  /** The word being typed, replaced by what it was completed to. */
  function completeWord(label: string): string {
    const words = typed.trimEnd().split(/\s+/);

    words[words.length - 1] = label;

    return `${words.join(' ')} `;
  }

  function addTag(slug: string, name: string, dropWord: boolean) {
    if (!tags.some((tag) => tag.slug === slug)) {
      tags = [...tags, { slug, name }];
    }

    set(dropWord ? typed.trimEnd().split(/\s+/).slice(0, -1).join(' ') : typed);
  }

  function activate(option: SearchOption, elsewhere = false) {
    if (option.kind === 'result') {
      openRecipe(option.recipe.id, elsewhere);

      return;
    }

    const completion = option.completion;

    switch (completion.kind) {
      case 'recipe':
        openRecipe(completion.recipeId, elsewhere);
        break;
      case 'ingredient':
        set(completeWord(completion.label));
        break;
      case 'tag':
        addTag(completion.slug, completion.label, true);
        break;
      case 'refinement':
        set(
          m['search.refinement.query']({ name: completion.label, minutes: completion.maxMinutes })
        );
        break;
    }
  }

  return {
    get typed() {
      return typed;
    },
    get applied() {
      return applied;
    },
    get tags() {
      return tags;
    },
    get highlighted() {
      return highlighted;
    },
    set highlighted(index: number) {
      highlighted = index;
    },
    get asking() {
      return asking;
    },
    get options() {
      return options;
    },

    type,
    set,
    addTag,
    activate,

    remove(chip: SearchChip) {
      set(withoutChip(applied, chip));
    },

    removeTag(slug: string) {
      tags = tags.filter((tag) => tag.slug !== slug);
      set(typed);
    },

    /** The reader turned the correction down: search for what they typed. */
    searchAsTyped() {
      asTypedFor = applied;
      search(applied);
    },

    /**
     * A completion taken into the field without going anywhere: a recipe's
     * name is typed out rather than opened, the rest do what choosing them does.
     */
    accept(completion: Completion) {
      if (completion.kind === 'recipe') {
        set(completion.label);
      } else {
        activate({ key: '', index: -1, kind: 'completion', completion });
      }
    },

    /** Closed: nothing typed survives, and nothing in flight may land. */
    reset() {
      cancelPending();
      typed = '';
      applied = '';
      tags = [];
      asTypedFor = null;
      completions.clear();
    },

    /** The component is going away. */
    dispose: cancelPending
  };
}

export type SearchSession = ReturnType<typeof createSearchSession>;
