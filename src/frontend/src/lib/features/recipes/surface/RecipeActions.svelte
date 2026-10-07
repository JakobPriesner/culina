<script lang="ts">
  import { resolve } from '$app/paths';
  import { ActionMenu, Button, IconButton } from '$ds';

  import { m } from '$shell/i18n';

  /** Actions beside the title; each is shown only if the page passed its callback. */
  interface Props {
    recipeId: string;
    /** Nothing is shown while cooking. */
    cooking: boolean;
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

  const inMenu = $derived(
    !cooking && Boolean(editable || onaddtocookbook || onaddtoplan || onshare || oncopy)
  );
  const hasActions = $derived(inMenu || Boolean(!cooking && (onaddtolist || ondelete)));
</script>

<!-- The shopping list is the frequent action and stays outside the menu; the occasional ones go in it. -->
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
            <path d="M4 8h16l-1.4 10a2 2 0 0 1-2 1.7H7.4a2 2 0 0 1-2-1.7Z" />
            <path d="M9 8 12 3l3 5" />
          </svg>
        {/snippet}

        {m['shopping.addToList']()}
      </Button>
    {/if}

    {#if inMenu}
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
              <rect x="8.5" y="8.5" width="11" height="12" rx="2" />
              <path d="M15.5 8.5V5.5a2 2 0 0 0-2-2h-7a2 2 0 0 0-2 2v9a2 2 0 0 0 2 2h2" />
            </svg>

            {m['recipe.copy.action']()}
          </button>
        {/if}

        <!-- A link, so the editor can open in a second tab. -->
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

    <!-- Last and outside the menu so a reach for "Edit" never lands on it. -->
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

    /* Overlay the photo so a long title keeps the whole line; the surface sets `.photographed` and the positioning context. */
    :global(.photographed) .actions {
      position: absolute;
      inset-block-start: var(--space-2);
      inset-inline-end: var(--space-2);
      z-index: var(--z-sticky);
    }

    /* Collapse the labelled button to an icon; its aria-label stays the full name. */
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
