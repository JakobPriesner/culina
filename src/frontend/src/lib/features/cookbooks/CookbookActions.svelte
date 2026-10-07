<script lang="ts">
  import { ActionMenu, Button, IconButton } from '$ds';
  import { m } from '$shell/i18n';

  /** Filling the shelf and shopping stay visible; edit and delete live in the menu. */
  interface Props {
    /** A shelf that fills itself is edited by its rules, not by hand. */
    automatic: boolean;
    addingToList: boolean;
    onedit: () => void;
    onpick: () => void;
    onaddtolist: () => void;
    ondelete: () => void;
  }

  let { automatic, addingToList, onedit, onpick, onaddtolist, ondelete }: Props = $props();
</script>

<div class="actions">
  {#if automatic}
    <Button variant="primary" onclick={onedit}>
      {#snippet icon()}
        <svg
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="1.8"
          stroke-linecap="round"
          stroke-linejoin="round"
          aria-hidden="true"
        >
          <path d="M4 7h10M18 7h2M4 17h2M10 17h10" />
          <circle cx="16" cy="7" r="2" />
          <circle cx="8" cy="17" r="2" />
        </svg>
      {/snippet}

      {m['cookbooks.rules.edit']()}
    </Button>
  {:else}
    <Button variant="primary" onclick={onpick}>
      {#snippet icon()}
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
          <path d="M8 3.5v17M13 9v6M10 12h6" />
        </svg>
      {/snippet}

      {m['cookbooks.addRecipes.action']()}
    </Button>
  {/if}

  <Button loading={addingToList} onclick={onaddtolist}>
    {#snippet icon()}
      <svg
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        stroke-width="1.8"
        stroke-linecap="round"
        stroke-linejoin="round"
        aria-hidden="true"
      >
        <path d="M4 8h16l-1.4 10a2 2 0 0 1-2 1.7H7.4a2 2 0 0 1-2-1.7Z" />
        <path d="M9 8 12 3l3 5" />
      </svg>
    {/snippet}

    {m['cookbooks.shopping.add']()}
  </Button>

  <ActionMenu minWidth="13rem">
    {#snippet trigger({ popovertarget })}
      <IconButton bordered label={m['cookbooks.moreActions']()} {popovertarget}>
        <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
          <circle cx="12" cy="5" r="1.6" />
          <circle cx="12" cy="12" r="1.6" />
          <circle cx="12" cy="19" r="1.6" />
        </svg>
      </IconButton>
    {/snippet}

    {#if !automatic}
      <button class="item" type="button" onclick={onedit}>
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

        {m['cookbooks.edit.title']()}
      </button>

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
        <path d="M4 7h16M9 7V4h6v3M7 7l1 13h8l1-13" />
        <path d="M10 11v5M14 11v5" />
      </svg>

      {m['cookbooks.delete.action']()}
    </button>
  </ActionMenu>
</div>

<style>
  .actions {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    justify-content: flex-end;
    gap: var(--space-2);
  }

  @media (width < 36rem) {
    .actions {
      width: 100%;
      justify-content: flex-start;
    }
  }
</style>
