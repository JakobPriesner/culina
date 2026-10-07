<script lang="ts">
  import { resolve } from '$app/paths';
  import { ActionMenu, Button, IconButton } from '$ds';

  import { m } from '$shell/i18n';

  /**
   * What can be done with the recipe on screen, beside its title.
   *
   * Offered to whoever may do it and to nobody else: each action is there
   * because the page handed in the callback for it. The surface decides which
   * of them it has, and this only lays them out.
   */
  interface Props {
    recipeId: string;
    /** Nothing at all while cooking: hands are full. */
    cooking: boolean;
    /** Whether to offer the way back into the editor. */
    editable: boolean;
    onaddtolist?: () => void;
    onaddtocookbook?: () => void;
    onaddtoplan?: () => void;
    onshare?: () => void;
    oncopy?: () => void;
    ondelete?: () => void;
  }

  let {
    recipeId,
    cooking,
    editable,
    onaddtolist,
    onaddtocookbook,
    onaddtoplan,
    onshare,
    oncopy,
    ondelete
  }: Props = $props();

  /** The occasional actions, which go in the menu. */
  const inMenu = $derived(
    !cooking && Boolean(editable || onaddtocookbook || onaddtoplan || onshare || oncopy)
  );
  const hasActions = $derived(inMenu || Boolean(!cooking && (onaddtolist || ondelete)));
</script>

<!--
  Everything except cooking, beside the title.

  One rank in one place. What used to be here was a bare "Edit" link while
  four unrelated actions floated at the bottom of the screen, which meant
  the page answered "what can I do with this recipe" in two places and in
  neither of them completely.

  The shopping list keeps its own control because it is the weekly loop —
  read a recipe, put it on the list — and a loop that runs twice a week
  does not belong behind a menu. The other three are occasional, so they
  go in one, the way a document's rarely-used actions do everywhere else.

  Nothing at all while cooking: hands are full, and the only correct edit
  then is the one made to the pan.
-->
{#if hasActions}
  <div class="actions">
    {#if onaddtolist}
      <Button label={m['shopping.addToList']()} onclick={onaddtolist}>
        {#snippet icon()}
          <svg
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="1.8"
            stroke-linecap="round"
            stroke-linejoin="round"
          >
            <!-- The same basket the shopping tab is marked with. -->
            <path d="M4 8h16l-1.4 10a2 2 0 0 1-2 1.7H7.4a2 2 0 0 1-2-1.7Z" />
            <path d="M9 8 12 3l3 5" />
          </svg>
        {/snippet}

        {m['shopping.addToList']()}
      </Button>
    {/if}

    {#if inMenu}
      <!-- Opening towards the middle of the page: the group is at the
           inline end of a full-width row, and a panel that preferred the
           other side would be hanging off the edge of the screen. -->
      <ActionMenu>
        {#snippet trigger({ popovertarget })}
          <IconButton bordered label={m['recipe.moreActions']()} {popovertarget}>
            <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
              <circle cx="12" cy="5" r="1.6" />
              <circle cx="12" cy="12" r="1.6" />
              <circle cx="12" cy="19" r="1.6" />
            </svg>
          </IconButton>
        {/snippet}

        {#if onaddtoplan}
          <button class="item" type="button" onclick={onaddtoplan}>
            <svg
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="1.8"
              stroke-linecap="round"
              stroke-linejoin="round"
              aria-hidden="true"
            >
              <rect x="3.5" y="5.5" width="17" height="15" rx="2" />
              <path d="M8 3.5v4M16 3.5v4M3.5 10h17" />
              <path d="m9 15 2 2 4-4" />
            </svg>

            {m['plan.recipe.action']()}
          </button>
        {/if}

        {#if onaddtocookbook}
          <button class="item" type="button" onclick={onaddtocookbook}>
            <svg
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="1.8"
              stroke-linecap="round"
              stroke-linejoin="round"
              aria-hidden="true"
            >
              <!-- A book, closed, spine to the left. -->
              <path
                d="M6.5 3.5H17a1 1 0 0 1 1 1v15a1 1 0 0 1-1 1H6.5a2 2 0 0 1-2-2v-13a2 2 0 0 1 2-2Z"
              />
              <path d="M8 3.5v17" />
            </svg>

            {m['cookbooks.add.action']()}
          </button>
        {/if}

        {#if onshare}
          <button class="item" type="button" onclick={onshare}>
            <svg
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="1.8"
              stroke-linecap="round"
              stroke-linejoin="round"
              aria-hidden="true"
            >
              <!-- Three nodes and the two threads between them: the shape
                   every platform's share control has settled on, so
                   nobody has to learn what this one means. -->
              <circle cx="18" cy="5" r="2.5" />
              <circle cx="6" cy="12" r="2.5" />
              <circle cx="18" cy="19" r="2.5" />
              <path d="M8.2 10.8 15.8 6.4" />
              <path d="m8.2 13.2 7.6 4.4" />
            </svg>

            {m['recipe.share.action']()}
          </button>
        {/if}

        {#if oncopy}
          <button class="item" type="button" onclick={oncopy}>
            <svg
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="1.8"
              stroke-linecap="round"
              stroke-linejoin="round"
              aria-hidden="true"
            >
              <!-- Two sheets, one over the other. -->
              <rect x="8.5" y="8.5" width="11" height="12" rx="2" />
              <path d="M15.5 8.5V5.5a2 2 0 0 0-2-2h-7a2 2 0 0 0-2 2v9a2 2 0 0 0 2 2h2" />
            </svg>

            {m['recipe.copy.action']()}
          </button>
        {/if}

        <!-- A link, not a button, and the other end of the editor's
               "← Done": a recipe somebody is about to rewrite is one they
               open in a second tab beside the one they are reading. -->
        {#if editable}
          <a class="item" href={resolve('/(app)/recipes/[recipeId]/edit', { recipeId })}>
            <svg
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="1.8"
              stroke-linecap="round"
              stroke-linejoin="round"
              aria-hidden="true"
            >
              <path d="M4 20h4L19 9a2.1 2.1 0 0 0-3-3L5 17v3Z" />
              <path d="m15 6 3 3" />
            </svg>

            {m['editor.edit']()}
          </a>
        {/if}
      </ActionMenu>
    {/if}

    <!-- On the page rather than in the menu, so nobody has to go looking
         for it — but last in the row, outside the menu, so the hand
         reaching for "Edit" never lands on it. The dialog it opens is
         the question; this only offers. -->
    {#if ondelete}
      <IconButton bordered label={m['recipe.delete.action']()} onclick={ondelete}>
        <svg
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="1.8"
          stroke-linecap="round"
          stroke-linejoin="round"
        >
          <path d="M4 7h16M10 11v6M14 11v6" />
          <path d="M6 7l1 12a2 2 0 0 0 2 2h6a2 2 0 0 0 2-2l1-12" />
          <path d="M9 7V4.5a1 1 0 0 1 1-1h4a1 1 0 0 1 1 1V7" />
        </svg>
      </IconButton>
    {/if}
  </div>
{/if}

<style>
  .actions {
    display: flex;
    align-items: center;
    gap: var(--space-2);
  }

  @media (width < 52rem) {
    .actions {
      gap: var(--space-1);
    }

    /* The photo already owns this strip of the first screen. Putting the two
       small controls on it keeps them immediately available and gives even a
       long recipe name the whole line below. Recipes without a photo keep the
       actions beside their title, where there is no image to carry them. The
       surface marks a recipe with a photograph, and positions this against
       itself. */
    :global(.photographed) .actions {
      position: absolute;
      inset-block-start: var(--space-2);
      inset-inline-end: var(--space-2);
      z-index: var(--z-sticky);
    }

    /* The labelled supporting action becomes the same familiar basket icon
       used by the shopping tab. Its aria-label remains the complete name. */
    .actions :global(.button) {
      width: var(--control-md);
      padding-inline: 0;
    }

    .actions :global(.button .label) {
      display: none;
    }
  }

  @media print {
    .actions {
      display: none;
    }
  }
</style>
