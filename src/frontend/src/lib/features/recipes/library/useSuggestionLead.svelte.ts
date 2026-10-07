import { recipes } from '$features/recipes/stores/recipes.svelte';
import { recallRanking, rememberRanking } from '$features/recipes/stores/rankingHint';
import { suggestions } from '$features/recipes/stores/suggestions.svelte';
import type { Suggestion } from '$features/recipes/types';
import { m } from '$shell/i18n';
import { toaster } from '$shell/toaster.svelte';

/**
 * The shortlist at the top of the page. Asked once per household.
 *
 * Five, which is the server's own default and about as many answers as
 * anybody holds in their head while deciding what to eat. It was three when
 * the panel showed one and the other two were only there so that dismissing
 * the leader revealed the next instead of emptying it; now every one of them
 * is walked past, and a shortlist you reach the end of in two swipes is not
 * one. Twelve is the ceiling and would be a feed.
 */
export const featuredQuery = { limit: 5 } as const;

/** What the lead needs to know about the page it is on. */
interface Page {
  readonly householdId: () => string | null;
  /** Whether a filter is on, which takes the lead away. */
  readonly filtered: () => boolean;
}

/**
 * What leads the library: the answer to "what should I cook?", best first,
 * and what the ranking says about this kitchen.
 */
export function useSuggestionLead(page: Page) {
  const shortlist = $derived(suggestions.for(page.householdId(), featuredQuery));

  const topSuggestion = $derived(shortlist[0]);

  /**
   * What the ranking said about this kitchen the last time this device asked.
   *
   * Read once per household and then held for the visit, deliberately: it is
   * not replaced when the fresh answer arrives. A list fetched in one order and
   * re-fetched in another is a page that rearranges itself under somebody who
   * has started reading it. When the two disagree — about once in the life of a
   * kitchen — this visit keeps the order it began with, and the next one has
   * the new answer.
   */
  const remembered = $derived.by(() => {
    const householdId = page.householdId();

    return householdId ? recallRanking(householdId) : null;
  });

  /**
   * Whether the ranking has anything true to say about this kitchen yet.
   *
   * Used instead of counting cook-log entries against a threshold, and it is
   * the better signal: a reason exists exactly when one term of the score
   * actually dominated, which is the same thing as "there is enough history
   * here for the order to mean something". A brand-new kitchen gets the app it
   * has always had, and nothing had to guess a number.
   */
  const ranks = $derived(remembered ?? topSuggestion?.reason != null);

  /**
   * What leads the page.
   *
   * The panel has always been here; what filled it was the first recipe with a
   * photograph, which is an accident rather than an answer. Then it was the
   * best suggestion, with the reason as its eyebrow. Now it is the whole
   * shortlist, one at a time, because "not tonight, what else?" is the ordinary
   * reply to a suggestion and the only way to say it was to say "never again".
   *
   * The photograph is still the fallback, and it is handed over as a suggestion
   * with no reason — which is exactly what it is: something shown with nothing
   * to say about why. The deck then draws it as the panel has always looked,
   * with the fixed eyebrow and nothing to walk.
   */
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

  // Kept for the next visit, and only from an answer that actually came back:
  // a failed shortlist says nothing about the kitchen, and remembering it as
  // "nothing to say" would hold a ranked kitchen in recent order until the
  // next success.
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

  /**
   * "Not this one."
   *
   * Optimistic, and answered with an Undo toast rather than a confirmation —
   * the same shape as moving a planned meal, because a dismissal is a small
   * reversible decision and a dialog would make it feel like a large one. The
   * panel refills from the next answer rather than jumping to whatever was
   * second, so nothing moves under the thumb that just tapped.
   */
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

  /** What asks for the next of the shortlist, or nothing when there is no more. */
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
