import { recipes } from '$features/recipes/stores/recipes.svelte';
import { recallRanking, rememberRanking } from '$features/recipes/stores/rankingHint';
import { suggestions } from '$features/recipes/stores/suggestions.svelte';
import type { Suggestion } from '$features/recipes/types';
import { m } from '$shell/i18n';
import { toaster } from '$shell/toaster.svelte';

/** The shortlist query: five matches the server default and what a person can weigh at once. */
export const featuredQuery = { limit: 5 } as const;

interface Page {
  readonly householdId: () => string | null;
  /** A filter being on takes the lead away. */
  readonly filtered: () => boolean;
}

/** The library's lead: the ranked "what should I cook?" shortlist and whether ranking has history to go on. */
export function useSuggestionLead(page: Page) {
  const shortlist = $derived(suggestions.for(page.householdId(), featuredQuery));

  const topSuggestion = $derived(shortlist[0]);

  // Held for the visit and not replaced by the fresh answer, so the page doesn't reorder under the reader.
  const remembered = $derived.by(() => {
    const householdId = page.householdId();

    return householdId ? recallRanking(householdId) : null;
  });

  // A reason exists only when one score term dominated, i.e. there is enough history; no threshold to guess.
  const ranks = $derived(remembered ?? topSuggestion?.reason != null);

  // Fallback: the first recipe with a photo, passed as a suggestion with no reason.
  const fallback = $derived(
    page.filtered() || shortlist.length > 0
      ? undefined
      : recipes.items.find((recipe) => recipe.imageId !== null)
  );

  const lead = $derived<readonly Suggestion[]>(
    page.filtered()
      ? []
      : shortlist.length > 0
        ? shortlist
        : fallback
          ? [{ ...fallback, reason: null }]
          : []
  );

  $effect(() => {
    const householdId = page.householdId();

    if (householdId) {
      void suggestions.ask(householdId, featuredQuery);
    }
  });

  // Only remember a successful answer; a failure would pin a ranked kitchen to recent order.
  $effect(() => {
    const householdId = page.householdId();

    if (householdId && suggestions.statusOf(householdId, featuredQuery) === 'ready') {
      rememberRanking(householdId, topSuggestion?.reason != null);
    }
  });

  async function restore(recipeId: string) {
    await suggestions.restore(recipeId);

    const householdId = page.householdId();

    if (householdId) {
      void suggestions.ask(householdId, featuredQuery);
    }
  }

  /** Dismisses a suggestion with an Undo toast rather than a confirmation. */
  async function hide(recipeId: string) {
    const failure = await suggestions.dismiss(recipeId);

    if (failure) {
      toaster.show({ message: () => m['suggestions.dismissFailed'](), tone: 'danger' });

      return;
    }

    toaster.show({
      message: () => m['suggestions.dismissed'](),
      tone: 'success',
      action: {
        label: () => m['suggestions.restore'](),
        run: () => void restore(recipeId)
      }
    });
  }

  /** Loads more of the shortlist, or undefined when there is no more. */
  function moreHandler() {
    const householdId = page.householdId();

    return householdId && suggestions.hasMore(householdId, featuredQuery)
      ? () => void suggestions.more(householdId, featuredQuery)
      : undefined;
  }

  return {
    get shortlist() {
      return shortlist;
    },
    get ranks() {
      return ranks;
    },
    get lead() {
      return lead;
    },
    hide,
    moreHandler
  };
}
