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
    /** Already on the list: the button says so, and still opens the list sheet to add more. */
    listed?: boolean;
    onaddtocookbook?: () => void;
    onaddtoplan?: () => void;
    /**
     * Desktop only: the page's one filled action beside the title, where the reading starts, instead of a bar
     * floating over the steps. Phones keep it docked at the bottom (`CookingAction`).
     */
    onstartcooking?: () => void;
    resuming?: boolean;
    /** Desktop only, in the menu: cooked without cook mode. */
    onmadeit?: () => void;
    onshare?: () => void;
    oncopy?: () => void;
    ondelete?: () => void;
  }

  let {
    recipeId,
    cooking,
    editable,
    onaddtolist,
    listed = false,
    onaddtocookbook,
    onaddtoplan,
    onstartcooking,
    resuming = false,
    onmadeit,
    onshare,
    oncopy,
    ondelete
  }: Props = $props();

  const inMenu = $derived(
    !cooking && Boolean(onaddtocookbook || onshare || oncopy || ondelete || onmadeit)
  );
  const hasActions = $derived(
    !cooking && Boolean(inMenu || onaddtolist || onaddtoplan || editable || onstartcooking)
  );
</script>

<!-- The loop's next steps (plan, shop) and editing stay outside the menu; the occasional ones go in it, and deleting comes last there, apart and in red. -->
{#if hasActions}
  <div class="actions" class:edit-in-menu={inMenu && editable}>
    {#if onstartcooking}
      <span class="wide-only">
        <Button variant="primary" onclick={onstartcooking}>
          {resuming ? m['cooking.resume']() : m['recipe.startCooking']()}
        </Button>
      </span>
    {/if}

    {#if onaddtoplan}
      <Button onclick={onaddtoplan}>
        {#snippet icon()}
          <svg
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="1.8"
            stroke-linecap="round"
            stroke-linejoin="round"
          >
            <rect x="3.5" y="5.5" width="17" height="15" rx="2" />
            <path d="M8 3.5v4M16 3.5v4M3.5 10h17" />
            <path d="m9 15 2 2 4-4" />
          </svg>
        {/snippet}

        <span class="long">{m['plan.recipe.action']()}</span>
        <span class="short">{m['plan.recipe.short']()}</span>
      </Button>
    {/if}

    {#if onaddtolist}
      <Button onclick={onaddtolist}>
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
            {#if listed}
              <path d="m9 13.5 2 2 4-4" />
            {:else}
              <path d="M9 8 12 3l3 5" />
            {/if}
          </svg>
        {/snippet}

        <span class="long">{listed ? m['shopping.onList']() : m['shopping.addToList']()}</span>
        <span class="short"
          >{listed ? m['shopping.onList.short']() : m['shopping.addToList.short']()}</span
        >
      </Button>
    {/if}

    <!-- A link, so the editor can open in a second tab. -->
    {#if editable}
      <span class="edit"
        ><Button
          label={m['editor.edit']()}
          href={resolve('/(app)/recipes/[recipeId]/edit', { recipeId })}
        >
          {#snippet icon()}
            <svg
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="1.8"
              stroke-linecap="round"
              stroke-linejoin="round"
            >
              <path d="M4 20h4L19 9a2.1 2.1 0 0 0-3-3L5 17v3Z" />
              <path d="m15 6 3 3" />
            </svg>
          {/snippet}

          {m['editor.edit']()}
        </Button></span
      >
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

        <!-- Only on the narrowest phones, where the pencil beside the menu no longer fits the row. -->
        {#if editable}
          <span class="narrow-edit">
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
          </span>
        {/if}

        {#if onmadeit}
          <span class="wide-only">
            <button class="item" type="button" onclick={onmadeit}>
              <svg
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                stroke-width="1.8"
                stroke-linecap="round"
                stroke-linejoin="round"
                aria-hidden="true"
              >
                <circle cx="12" cy="12" r="9" />
                <path d="m8.5 12.5 2.5 2.5 4.5-5" />
              </svg>

              {m['cooking.madeIt']()}
            </button>
          </span>
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

        <!-- Last, apart and in red; "Edit" is outside the menu, so a reach for it never lands here. -->
        {#if ondelete}
          {#if onaddtocookbook || onshare || oncopy}
            <hr class="separator" />
          {/if}

          <button class="item danger" type="button" onclick={ondelete}>
            <svg
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="1.8"
              stroke-linecap="round"
              stroke-linejoin="round"
              aria-hidden="true"
            >
              <path d="M4 7h16M10 11v6M14 11v6" />
              <path d="M6 7l1 12a2 2 0 0 0 2 2h6a2 2 0 0 0 2-2l1-12" />
              <path d="M9 7V4.5a1 1 0 0 1 1-1h4a1 1 0 0 1 1 1V7" />
            </svg>

            {m['recipe.delete.action']()}
          </button>
        {/if}
      </ActionMenu>
    {/if}
  </div>
{/if}

<style>
  /* Wraps rather than running off the screen when a label is longer (on the list) or text is enlarged. */
  .actions {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-2);
  }

  /* The name follows what is on screen: the long wording, or the short one that fits a phone beside the others. */
  .short {
    display: none;
  }

  /*
   * Short of a very wide screen, the loop's two next steps keep a short word (an icon alone left "basket" and
   * "calendar" to guess at) and editing collapses to a circle like the menu's, its aria-label the full name. On a
   * desktop that keeps "Kochen starten" and the rest on the title's line, so the ingredients start on the first
   * screen (culina-v2-6ilt.17).
   */
  @media (width < 100rem) {
    .long {
      display: none;
    }

    .short {
      display: inline;
    }

    /* Whole words on one line (no mid-word breaks), with the side room a short word needs rather than a sentence's. */
    .actions :global(.button) {
      flex-shrink: 0;
      padding-inline: var(--space-3);
      white-space: nowrap;
    }

    .edit :global(.button) {
      width: var(--control-md);
      padding-inline: 0;
    }

    .edit :global(.button .label) {
      display: none;
    }
  }

  @media (width < 52rem) {
    .actions {
      gap: var(--space-1);
    }
  }

  .narrow-edit,
  .wide-only {
    display: none;
  }

  @media (min-width: 64rem) {
    .wide-only {
      display: contents;
    }
  }

  /* Two short labels and two circles need about 383px; narrower (a 360 or 320px phone), editing joins the menu, first in it. */
  @media (width < 24rem) {
    .edit-in-menu .edit {
      display: none;
    }

    .narrow-edit {
      display: contents;
    }
  }

  @media print {
    .actions {
      display: none;
    }
  }
</style>
