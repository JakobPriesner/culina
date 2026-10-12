<script lang="ts">
  import { goto } from '$app/navigation';
  import { resolve } from '$app/paths';
  import { page } from '$app/state';
  import { ActionMenu } from '$ds';

  import HouseholdChoices from '$features/auth/HouseholdChoices.svelte';
  import NewHouseholdSheet from '$features/auth/NewHouseholdSheet.svelte';
  import { session } from '$features/auth/session.svelte';
  import { signOut } from '$features/auth/signOut';

  import { m } from '$shell/i18n';
  import LocalePicker from '../LocalePicker.svelte';
  import { inSettings } from '../navigation';
  import ThemeChoice from '../ThemeChoice.svelte';
  import { update } from '../updates.svelte';

  /**
   * The person's corner of the header, in place of a Settings destination: who is signed in, which
   * household is on screen, the two preferences changed on the spot, and the doors to settings and
   * out. Theme and language keep the panel open, so the change is seen; a household or a door closes it.
   */
  interface Props {
    onswitch: () => void;
  }

  let { onswitch }: Props = $props();

  let creating = $state(false);

  const user = $derived(session.user);

  /** No destination is lit on a settings page, so the way there is. */
  const here = $derived(inSettings(page.url.pathname));

  const themeLabel = $props.id();

  /** ⌘, / Ctrl-, opens settings, as in most desktop apps. */
  function shortcut(event: KeyboardEvent) {
    if (user && event.key === ',' && (event.metaKey || event.ctrlKey)) {
      event.preventDefault();
      void goto(resolve('/(app)/me'));
    }
  }
</script>

<svelte:window onkeydown={shortcut} />

{#if user}
  <ActionMenu minWidth="16rem" maxWidth="20rem" wrap>
    {#snippet trigger({ popovertarget })}
      <button
        type="button"
        class="trigger"
        class:here
        {popovertarget}
        aria-label={update.ready ? m['account.openUpdate']() : m['account.open']()}
        title={m['account.open']()}
      >
        <svg
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="1.8"
          stroke-linecap="round"
          aria-hidden="true"
        >
          <circle cx="12" cy="8" r="4" />
          <path d="M4.5 20c.8-3.4 3.8-5.5 7.5-5.5s6.7 2.1 7.5 5.5" />
        </svg>
        {#if update.ready}<span class="dot" aria-hidden="true"></span>{/if}
      </button>
    {/snippet}

    <div class="identity">
      <p class="name">{user.displayName}</p>
      <p class="email">{user.email}</p>
    </div>

    {#if session.activeHousehold}
      <hr class="separator" />
      <HouseholdChoices {onswitch} oncreate={() => (creating = true)} />
    {/if}

    <hr class="separator" />

    <div class="setting" role="group" aria-labelledby={themeLabel}>
      <span id={themeLabel}>{m['me.theme']()}</span>
      <ThemeChoice compact />
    </div>

    <div class="setting">
      <span aria-hidden="true">{m['locale.label']()}</span>
      <LocalePicker compact />
    </div>

    <hr class="separator" />

    {#if update.ready}
      <button type="button" class="item" onclick={() => update.apply()}>
        <span class="mark" aria-hidden="true"><span class="pending"></span></span>
        <span class="grow">{m['app.update.available']()}</span>
        <span class="reload">{m['app.update.reload']()}</span>
      </button>
    {/if}

    <a class="item" href={resolve('/(app)/me')} aria-keyshortcuts="Meta+, Control+,">
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
          d="M12.22 2h-.44a2 2 0 0 0-2 2v.18a2 2 0 0 1-1 1.73l-.43.25a2 2 0 0 1-2 0l-.15-.08a2 2 0 0 0-2.73.73l-.22.38a2 2 0 0 0 .73 2.73l.15.1a2 2 0 0 1 1 1.72v.51a2 2 0 0 1-1 1.74l-.15.09a2 2 0 0 0-.73 2.73l.22.38a2 2 0 0 0 2.73.73l.15-.08a2 2 0 0 1 2 0l.43.25a2 2 0 0 1 1 1.73V20a2 2 0 0 0 2 2h.44a2 2 0 0 0 2-2v-.18a2 2 0 0 1 1-1.73l.43-.25a2 2 0 0 1 2 0l.15.08a2 2 0 0 0 2.73-.73l.22-.39a2 2 0 0 0-.73-2.73l-.15-.08a2 2 0 0 1-1-1.74v-.5a2 2 0 0 1 1-1.74l.15-.09a2 2 0 0 0 .73-2.73l-.22-.38a2 2 0 0 0-2.73-.73l-.15.08a2 2 0 0 1-2 0l-.43-.25a2 2 0 0 1-1-1.73V4a2 2 0 0 0-2-2z"
        />
        <circle cx="12" cy="12" r="3" />
      </svg>
      <span class="grow">{m['me.title']()}</span>
      <kbd class="hint" aria-hidden="true">⌘,</kbd>
    </a>

    <button type="button" class="item" onclick={() => void signOut()}>
      <svg
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        stroke-width="1.8"
        stroke-linecap="round"
        stroke-linejoin="round"
        aria-hidden="true"
      >
        <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4M16 17l5-5-5-5M21 12H9" />
      </svg>
      <span>{m['auth.signOut']()}</span>
    </button>
  </ActionMenu>

  <NewHouseholdSheet
    open={creating}
    onclose={() => (creating = false)}
    oncreated={() => {
      creating = false;
      onswitch();
    }}
  />
{/if}

<style>
  /* Drawn like the search button beside it; the header capsule supplies the glass. */
  .trigger {
    position: relative;
    display: inline-flex;
    flex-shrink: 0;
    align-items: center;
    justify-content: center;
    min-width: var(--control-sm);
    min-height: var(--control-sm);
    padding: var(--space-2);
    border: 0;
    border-radius: var(--radius-full);
    background: transparent;
    color: var(--text);
    cursor: pointer;
    transition: background-color var(--duration-fast) var(--ease-out);
  }

  .trigger:hover {
    background: var(--surface-selected);
  }

  .trigger:active {
    background: var(--surface-hover);
  }

  .trigger svg {
    width: calc(var(--space-4) + var(--space-1));
    height: calc(var(--space-4) + var(--space-1));
  }

  /* Lit like the current destination's pill; hover keeps its own, lighter fill. */
  .here,
  .here:hover {
    background: var(--surface-selected);
    color: var(--accent);
  }

  .dot,
  .pending {
    width: var(--space-2);
    height: var(--space-2);
    border-radius: var(--radius-full);
    background: var(--accent);
  }

  .dot {
    position: absolute;
    inset-block-start: var(--space-1);
    inset-inline-end: var(--space-1);
    box-shadow: 0 0 0 2px var(--surface);
  }

  .identity {
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
    padding: var(--space-2) var(--space-3);
    min-width: 0;
  }

  .name {
    font-size: var(--text-sm);
    font-weight: var(--weight-semibold);
    overflow-wrap: anywhere;
  }

  /* Addresses are long and have no spaces to break at. */
  .email {
    color: var(--text-muted);
    font-size: var(--text-xs);
    overflow-wrap: anywhere;
  }

  .setting {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-2) var(--space-3);
    padding: var(--space-1) var(--space-3);
    font-size: var(--text-sm);
  }

  .grow {
    flex: 1;
  }

  .reload {
    color: var(--accent);
    font-weight: var(--weight-semibold);
  }

  .hint {
    color: var(--text-muted);
    font: inherit;
    font-size: var(--text-xs);
  }
</style>
