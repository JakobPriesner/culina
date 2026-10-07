import { m } from '$shell/i18n';

import type { createRecipeStore } from '../stores/recipes.svelte';
import type { Completion, RecipeSummary, SearchChip } from '../types';
import type { createCompletionStore } from './stores/completions.svelte';
import { withoutChip } from './wording';

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
  readonly focus: () => void;
  readonly openRecipe: (recipeId: string, elsewhere: boolean) => void;
}

/** The search box's text, applied query, tag filters and listbox options, changed together. */
export function createSearchSession({
  recipes,
  completions,
  householdId,
  focus,
  openRecipe
}: Deps) {
  let typed = $state('');
  /** What was last searched for, which the chips' spans refer to. */
  let applied = $state('');
  let tags = $state<{ slug: string; name: string }[]>([]);
  /** The query whose correction the reader turned down. */
  let asTypedFor = $state<string | null>(null);
  let highlighted = $state(-1);

  let suggesting: ReturnType<typeof setTimeout> | undefined;
  let searching: ReturnType<typeof setTimeout> | undefined;

  const asking = $derived(applied.trim().length > 0 || tags.length > 0);

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

  /** Two debounces: completions are cheap and follow the word, results wait for a finished thought. */
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

  /** Sets the box and searches immediately. */
  function set(value: string) {
    typed = value;
    highlighted = -1;
    cancelPending();
    void completions.complete(householdId(), value);
    search(value);
    focus();
  }

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

    searchAsTyped() {
      asTypedFor = applied;
      search(applied);
    },

    /** Takes a completion into the field: a recipe name is typed out, not opened. */
    accept(completion: Completion) {
      if (completion.kind === 'recipe') {
        set(completion.label);
      } else {
        activate({ key: '', index: -1, kind: 'completion', completion });
      }
    },

    /** Clears everything and cancels pending work. */
    reset() {
      cancelPending();
      typed = '';
      applied = '';
      tags = [];
      asTypedFor = null;
      completions.clear();
    },

    dispose: cancelPending
  };
}

export type SearchSession = ReturnType<typeof createSearchSession>;
