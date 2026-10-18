<script lang="ts">
  import { Button } from '$ds';

  import IdeaDraft from '$features/assistance/IdeaDraft.svelte';
  import { acceptEverything, toPatch } from '$features/assistance/draftToRecipe';
  import { session } from '$features/auth/session.svelte';
  import { m } from '$shell/i18n';
  import { preferences } from '$shell/preferences.svelte';

  import { recipePatchFromPaste } from '../create/recipePatchFromPaste';
  import PasteImport from '../editor/PasteImport.svelte';
  import type { Recipe } from '../types';

  /**
   * Fills a recipe that is only a title (planned as "Lentil dal" before it was written down) from pasted
   * text or a link, or from an idea, without making a second recipe: the same id keeps its place on the
   * plan, its notes and its cook history. Photos are read by a background import, which makes a new
   * recipe, so they are not offered here.
   */
  interface Props {
    recipe: Recipe;
    /** Applies the filled-in contents to this recipe. */
    onfill: (patch: Partial<Recipe>) => void | Promise<unknown>;
  }

  let { recipe, onfill }: Props = $props();

  let way = $state<'paste' | 'idea' | null>(null);
</script>

{#if way === 'paste'}
  <PasteImport
    open
    householdId={recipe.householdId}
    onimport={async (parsed) => {
      await onfill(recipePatchFromPaste(parsed));
      way = null;
      return true;
    }}
    oncancel={() => (way = null)}
  />
{:else if way === 'idea'}
  <IdeaDraft
    householdId={recipe.householdId}
    language={preferences.locale}
    onwritten={async (written) => {
      await onfill(toPatch(written, acceptEverything(written), recipe));
      way = null;
    }}
    oncancel={() => (way = null)}
  />
{:else}
  <div class="ways">
    <Button size="sm" onclick={() => (way = 'paste')}>{m['recipe.stub.paste']()}</Button>
    {#if session.user?.assistance.draft}
      <Button size="sm" onclick={() => (way = 'idea')}>{m['recipe.stub.idea']()}</Button>
    {/if}
  </div>
  <p class="hint">{m['recipe.stub.fillHint']()}</p>
{/if}

<style>
  .ways {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2);
  }

  .hint {
    margin: 0;
    color: var(--text-muted);
    font-size: var(--text-xs);
  }
</style>
