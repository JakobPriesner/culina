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

/** The edited recipe with its local journal, autosave and conflict handling. Has no effects or lifecycle calls, so tests can drive it unmounted. */
export function createRecipeDraft(recipeId: () => string) {
  let draft = $state<Recipe | null>(null);

  /** What is on screen exists only on this device. */
  let unsent = $state(false);

  /** The editor opened onto work a previous visit had not saved. */
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

    // Dropped only once the server has it; a failed save keeps the journal.
    if (!failure && session.user) {
      forget(session.user.userId, recipeId());
      unsent = false;
    }

    return failure;
  });

  /** Owning household when it is not the active one; such an inherited recipe cannot be saved, so the editor says so instead of opening. */
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

    if (session.user) {
      remember(session.user.userId, recipeId(), draft);
      unsent = true;
    }

    autosave.touch();
  }

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

    /** The status word beside the title: conflict wins, unsent work is never "Saved", a lost connection is not a failure. */
    get saveState() {
      return saveState;
    },

    get conflicted() {
      return autosave.failure !== null && changedElsewhere(autosave.failure);
    },

    change,

    /** Takes the loaded recipe once per recipe, so it never overwrites typing. What this device kept wins over the server copy. */
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
     * Mirrors a photo saved by its own endpoint, which bumps the version by one.
     * Steps exactly one (never the response's version) so a concurrent write still conflicts instead of being overwritten.
     */
    photoWritten(imageId: string | null) {
      if (!draft) {
        return;
      }

      draft = { ...draft, imageId, version: draft.version + 1 };

      // The journal is reopened in place of the server copy, so it needs the step too.
      if (unsent && session.user) {
        remember(session.user.userId, recipeId(), draft);
      }
    },

    /** The field keeps the typed text; the recipe keeps the last valid number. */
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

    /** One of two conflict exits (no auto-merge): the journal would otherwise re-open the conflicting text on every reload. */
    async keepMine() {
      const mine = draft;

      if (!mine) {
        return;
      }

      // Re-read to learn the other change's version, then write this text on top.
      await recipes.load(recipeId());

      const latest = latestLoaded();

      if (!latest) {
        return;
      }

      draft = { ...mine, version: latest.version };
      autosave.clear();
      // Clearing dropped the pending save; this text is owed again.
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

      // Not left to `takeLoaded`, which takes a recipe only once and would keep the conflicting version.
      draft = latest;
      unsent = false;
      recovered = false;
      typed = nothingTyped();
      autosave.clear();
    },

    dispose() {
      void autosave.flush();
      autosave.dispose();
    }
  };
}

export { adoptSaved };

export type RecipeDraft = ReturnType<typeof createRecipeDraft>;
