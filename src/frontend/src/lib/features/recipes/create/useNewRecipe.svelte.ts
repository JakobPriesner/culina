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

interface Page {
  readonly title: () => string;
  /** The share this page was opened for; forgotten once used. */
  readonly shareId: string | null;
}

/**
 * Creates a recipe from the page and opens the editor. Two steps (create with a title, then update with pasted
 * contents) so there is only one way to write a recipe.
 */
export function useNewRecipe(page: Page) {
  const submission = createSubmission();

  /** Made by a first attempt, so a retry updates it rather than making another. */
  let created: Recipe | null = null;

  onDestroy(() => submission.dispose());

  async function forgetShare() {
    if (page.shareId) await forgetSharedRecipe(page.shareId).catch(() => {});
  }

  async function createOnce(householdId: string, make: () => ReturnType<typeof recipes.create>) {
    return (created?.householdId === householdId ? created : null) ?? (await make());
  }

  async function openEditor(householdId: string, recipeId: string, title: string) {
    const userId = session.user?.userId;

    if (userId) {
      rememberLastDraft(userId, householdId, recipeId, title);
    }

    await forgetShare();
    await goto(resolve('/(app)/recipes/[recipeId]/edit', { recipeId }));
  }

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

  /** Same road as a paste: create with a title, then fill in. An idea has no source to compare and goes straight to the editor. */
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
