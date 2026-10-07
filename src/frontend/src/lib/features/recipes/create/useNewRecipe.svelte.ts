import { onDestroy } from 'svelte';

import { goto } from '$app/navigation';
import { resolve } from '$app/paths';
import { acceptEverything, toPatch, type Draft } from '$features/assistance/draftToRecipe';
import { drafts } from '$features/assistance/stores/drafts.svelte';
import { createSubmission } from '$features/auth/submission.svelte';
import { session } from '$features/auth/session.svelte';
import { rememberLastDraft } from '$features/recipes/editor/lastDraft';
import type { ParsedRecipe } from '$features/recipes/editor/parseRecipeText';
import { recipes } from '$features/recipes/stores/recipes.svelte';
import type { Recipe } from '$features/recipes/types';
import { forgetSharedRecipe } from '$features/import/sharedRecipe';
import { m } from '$shell/i18n';

import { recipePatchFromPaste } from './recipePatchFromPaste';

/** What starting a recipe needs to know about the page it is on. */
interface Page {
  /** What has been typed in the title field. */
  readonly title: () => string;
  /** The share this page was opened for, which is forgotten once it is used. */
  readonly shareId: string | null;
}

/**
 * Making a recipe from what the page has gathered, and opening it for editing.
 *
 * Two steps rather than one endpoint, because creating takes a title and
 * nothing else — which is the whole shape of starting a recipe here — and
 * pasted contents are an ordinary edit of a recipe that already exists. A
 * create-with-everything endpoint would be a second way to write a recipe,
 * and the second way is the one that drifts.
 */
export function useNewRecipe(page: Page) {
  const submission = createSubmission();

  /** Made by a first attempt, so a retry updates it rather than making another. */
  let created: Recipe | null = null;

  onDestroy(() => submission.dispose());

  async function forgetShare() {
    if (page.shareId) await forgetSharedRecipe(page.shareId).catch(() => {});
  }

  /** Creates the recipe, or reuses the one a failed earlier attempt made. */
  async function createOnce(householdId: string, make: () => ReturnType<typeof recipes.create>) {
    return (created?.householdId === householdId ? created : null) ?? (await make());
  }

  /** Remembers where this was left and opens the editor on it. */
  async function openEditor(householdId: string, recipeId: string, title: string) {
    const userId = session.user?.userId;

    if (userId) {
      rememberLastDraft(userId, householdId, recipeId, title);
    }

    await forgetShare();
    await goto(resolve('/(app)/recipes/[recipeId]/edit', { recipeId }));
  }

  /** A title, and perhaps a pasted block of recipe text. */
  async function start(named: string, pasted: ParsedRecipe | null) {
    const householdId = session.activeHouseholdId;

    if (!householdId) {
      return;
    }

    const title = named || m['import.paste.untitled']();
    let id: string | null = null;

    const ok = await submission.run(async () => {
      const outcome = await createOnce(householdId, () =>
        recipes.create(householdId, title, undefined, pasted?.sourceUrl)
      );

      if ('code' in outcome) {
        return outcome;
      }

      id = outcome.id;
      created = outcome;

      return pasted ? recipes.update({ ...outcome, ...recipePatchFromPaste(pasted) }) : null;
    });

    if (ok && id) {
      await openEditor(householdId, id, title);
    }

    return ok;
  }

  /**
   * The same road as a pasted recipe: create with a title, then fill it in.
   *
   * Intake has already compared the draft to its source. An idea has no
   * source to compare and goes straight to the editor. Both use the ordinary
   * create and update path, with the source link and draft provenance intact.
   */
  async function startFromDraft(written: Draft, sourceUrl?: string): Promise<boolean> {
    const householdId = session.activeHouseholdId;

    if (!householdId) {
      return false;
    }

    const title = written.title?.trim() || page.title().trim() || m['import.paste.untitled']();
    let id: string | null = null;

    const ok = await submission.run(async () => {
      const outcome = await createOnce(householdId, () =>
        recipes.create(householdId, title, written.draftId, sourceUrl || undefined)
      );

      if ('code' in outcome) {
        return outcome;
      }

      id = outcome.id;
      created = outcome;

      return recipes.update({
        ...outcome,
        ...toPatch(written, acceptEverything(written), outcome)
      });
    });

    if (ok) drafts.dismiss();

    if (ok && id) {
      await openEditor(householdId, id, title);
    }

    return ok;
  }

  return { submission, start, startFromDraft, forgetShare };
}
