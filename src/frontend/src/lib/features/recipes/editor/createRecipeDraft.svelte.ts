import { session } from '$features/auth/session.svelte';
import { forget, recall, remember } from '$features/recipes/editor/journal';
import { changedElsewhere, recipes } from '$features/recipes/stores/recipes.svelte';
import type { Recipe } from '$features/recipes/types';

import { createAutosave } from './autosave.svelte';
import {
  minutesFrom,
  nothingTyped,
  yieldFrom,
  type MinutesField,
  type TypedNumbers
} from './numbers';
import { adoptSaved } from './adoptSaved';
import { describeSave } from './describeSave';

/**
 * The recipe being edited, and everything that keeps it safe while it is.
 *
 * Holds the edited copy, the journal that keeps it on this device until the
 * server has it, the autosave that sends it, and the two ways out of a
 * conflict. It makes no effects and no lifecycle calls — the page decides when
 * `takeLoaded` runs and calls `dispose` on the way out — so it can be driven
 * from a test without mounting anything.
 */
export function createRecipeDraft(recipeId: () => string) {
  /** The edited copy. Null until the recipe has arrived. */
  let draft = $state<Recipe | null>(null);

  /** Whether what is on screen exists only on this device. */
  let unsent = $state(false);

  /** True when this editor opened onto work a previous visit had not saved. */
  let recovered = $state(false);

  let typed = $state<TypedNumbers>(nothingTyped());

  const autosave = createAutosave(async () => {
    if (!draft) {
      return null;
    }

    const sent = draft.version;
    const failure = await recipes.update(draft);

    if (!failure) {
      adopt(sent);
    }

    // Dropped only once the server has it. A failed save leaves the journal
    // exactly where it was, which is the whole point of writing it first.
    if (!failure && session.user) {
      forget(session.user.userId, recipeId());
      unsent = false;
    }

    return failure;
  });

  /**
   * The household it belongs to, when that is not the one being looked at.
   *
   * Such a recipe is inherited here, and not this household's to change: the
   * server would refuse every save. The link to this page is not offered for
   * it, but an address can still be typed or kept from before, so the editor
   * says so rather than opening a form whose every keystroke would fail.
   */
  const inheritedFrom = $derived(
    recipes.detail?.id === recipeId() && recipes.detail.householdId !== session.activeHouseholdId
      ? recipes.detail.householdId
      : null
  );

  function adopt(sent: number) {
    const saved = recipes.detail;

    if (!draft || saved?.id !== draft.id) {
      return;
    }

    const adopted = adoptSaved(draft, saved, sent);

    draft = adopted.recipe;

    if (adopted.linked) {
      autosave.touch();
    }
  }

  function change(patch: Partial<Recipe>) {
    if (!draft) {
      return;
    }

    draft = { ...draft, ...patch };
    recovered = false;

    // Written here first, synchronously, before anything is sent. The gap
    // between a keystroke and a save is where work goes missing.
    if (session.user) {
      remember(session.user.userId, recipeId(), draft);
      unsent = true;
    }

    autosave.touch();
  }

  /** The recipe in the store, once it is the one this editor is for. */
  function latestLoaded(): Recipe | null {
    const latest = recipes.detail;

    return latest && latest.id === recipeId() ? latest : null;
  }

  const saveState = $derived(
    describeSave({
      failure: autosave.failure,
      saving: autosave.state === 'saving',
      saved: autosave.state === 'saved',
      recovered,
      unsent
    })
  );

  return {
    get recipe() {
      return draft;
    },

    get inheritedFrom() {
      return inheritedFrom;
    },

    get typed() {
      return typed;
    },

    /**
     * What the small word beside the title says.
     *
     * In the order that matters. A conflict is never masked by anything
     * reassuring; work that has not reached the server never reads as "Saved";
     * and a lost connection reads as where the work is rather than as a
     * failure, because the work is not lost — it is on this device, and saying
     * "Could not save" about it is both alarming and untrue.
     *
     * At rest it says that the recipe saves itself, which is the one question
     * an editor with no Save button owes an answer to before anything has
     * happened.
     */
    get saveState() {
      return saveState;
    },

    get conflicted() {
      return autosave.failure !== null && changedElsewhere(autosave.failure);
    },

    change,

    /**
     * Taken once per recipe: after that the draft is what the author is
     * editing, and overwriting it from the store would delete what they just
     * typed.
     *
     * What this device kept wins over what the server has. It is newer by
     * definition — it exists precisely because it never reached the server —
     * and the alternative is opening an editor onto an older version of
     * somebody's own sentence.
     */
    takeLoaded() {
      const loaded = latestLoaded();

      if (!loaded || draft?.id === loaded.id || inheritedFrom) {
        return;
      }

      const kept = session.user ? recall(session.user.userId, recipeId()) : null;

      draft = kept?.recipe ?? loaded;
      unsent = kept !== null;
      recovered = kept !== null;
    },

    /**
     * Keeps the draft in step with a photo saved by its own endpoint.
     *
     * Not through `change`: that would write the whole recipe again for a
     * change it does not own. But the photo is part of the recipe, so writing
     * it moves the recipe on one version, and a draft left on the old one sent
     * a stale `If-Match` with every save after it — a 412 on each keystroke,
     * for a conflict nobody else caused.
     *
     * Exactly one step, never the version a response names. If somebody else
     * wrote in between, the server is further on than that, the next save is
     * refused, and the author is asked — rather than their change being
     * quietly saved over.
     */
    photoWritten(imageId: string | null) {
      if (!draft) {
        return;
      }

      draft = { ...draft, imageId, version: draft.version + 1 };

      // What this device kept is opened in place of the server's copy next
      // time, so it has to know about the step too.
      if (unsent && session.user) {
        remember(session.user.userId, recipeId(), draft);
      }
    },

    /**
     * The yield field keeps what was typed and the recipe keeps the last
     * number that made sense.
     */
    writeYield(text: string) {
      typed = { ...typed, yieldAmount: text };

      const value = yieldFrom(text);

      if (value !== null) {
        change({ yieldAmount: value });
      }
    },

    writeMinutes(which: MinutesField, text: string) {
      typed = { ...typed, [which]: text };

      const value = minutesFrom(text);

      if (value !== undefined) {
        change({ [which]: value });
      }
    },

    /**
     * Two ways out of a conflict, and no third.
     *
     * Merging two people's recipes automatically is a guess, and a guess about
     * somebody's dinner is worse than a question. So the choice is theirs — and
     * it has to be offered, because the journal keeps what was typed and would
     * otherwise show it again on every reload, conflicting again forever.
     */
    async keepMine() {
      const mine = draft;

      if (!mine) {
        return;
      }

      // Re-read to learn the version somebody else's change produced, then
      // write this text on top of it. Nothing of theirs is silently kept: they
      // were told to look, and this is the person looking.
      await recipes.load(recipeId());

      const latest = latestLoaded();

      if (!latest) {
        return;
      }

      draft = { ...mine, version: latest.version };
      autosave.clear();
      // Clearing forgave what was owed, and this text is owed again.
      autosave.touch();

      await autosave.flush();
    },

    async takeTheirs() {
      if (session.user) {
        forget(session.user.userId, recipeId());
      }

      await recipes.load(recipeId());

      const latest = latestLoaded();

      if (!latest) {
        return;
      }

      // Assigned here rather than left to `takeLoaded`, which deliberately
      // takes the recipe only once: it is what stops a save in flight from
      // overwriting what is being typed, and it would leave this showing the
      // version the conflict was about.
      draft = latest;
      unsent = false;
      recovered = false;
      // Their numbers, not the ones this browser was in the middle of typing.
      typed = nothingTyped();
      autosave.clear();
    },

    /** Sends what is owed on the way out, and stops the timer. */
    dispose() {
      void autosave.flush();
      autosave.dispose();
    }
  };
}

export { adoptSaved };

export type RecipeDraft = ReturnType<typeof createRecipeDraft>;
