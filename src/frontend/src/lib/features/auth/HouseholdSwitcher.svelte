<script lang="ts">
  import { resolve } from '$app/paths';
  import { Popover } from '$ds';

  import { m } from '$shell/i18n';
  import NewHouseholdSheet from './NewHouseholdSheet.svelte';
  import { session } from './session.svelte';

  /**
   * Which kitchen is on screen, and the way to another one.
   *
   * Beside the brand because it answers the same question the brand does —
   * where am I — and one level more precisely. It is there with a single
   * household too: the name says whose recipes these are, and "New household"
   * has to live somewhere a person would look for it.
   *
   * Switching only changes which household is being looked at. Where that
   * leaves the page is the shell's business, not this menu's.
   */
  interface Props {
    /** After the household on screen changed, by switching or by creating one. */
    onswitch: () => void;
  }

  let { onswitch }: Props = $props();

  let creating = $state(false);

  const active = $derived(session.activeHousehold);

  /** Closes the menu the choice was made in, then acts on it. */
  function choose(event: MouseEvent, run: () => void) {
    const panel = (event.currentTarget as HTMLElement).closest('[popover]');

    if (panel instanceof HTMLElement && typeof panel.hidePopover === 'function') {
      panel.hidePopover();
    }

    run();
  }

  function select(householdId: string) {
    if (householdId === session.activeHouseholdId) {
      return;
    }

    session.selectHousehold(householdId);
    onswitch();
  }
</script>

{#if active}
  <Popover shrinks>
    {#snippet trigger({ popovertarget })}
      <button
        type="button"
        class="current"
        {popovertarget}
        aria-label={m['household.switch.label']({ name: active.name })}
      >
        <span class="name">{active.name}</span>
        <svg
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="1.8"
          stroke-linecap="round"
          stroke-linejoin="round"
          aria-hidden="true"
        >
          <path d="m7 10 5 5 5-5" />
        </svg>
      </button>
    {/snippet}

    <div class="menu">
      <p class="heading">{m['household.switch.heading']()}</p>

      {#each session.households as household (household.householdId)}
        {@const current = household.householdId === active.householdId}
        <button
          type="button"
          class="item"
          aria-current={current || undefined}
          onclick={(e) => choose(e, () => select(household.householdId))}
        >
          <span class="mark" aria-hidden="true">
            {#if current}
              <svg
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                stroke-width="2"
                stroke-linecap="round"
                stroke-linejoin="round"
              >
                <path d="m5 12 5 5 9-10" />
              </svg>
            {/if}
          </span>
          <span class="label">
            <span class="title">{household.name}</span>
            {#if household.inheritsFrom[0]}
              <span class="inherits">
                {m['household.inherit.current']({ name: household.inheritsFrom[0].name })}
              </span>
            {/if}
          </span>
        </button>
      {/each}

      <hr class="separator" />

      <button type="button" class="item" onclick={(e) => choose(e, () => (creating = true))}>
        <span class="mark" aria-hidden="true">
          <svg
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="1.8"
            stroke-linecap="round"
          >
            <path d="M12 5v14M5 12h14" />
          </svg>
        </span>
        <span class="title">{m['household.new.title']()}</span>
      </button>

      <a
        class="item"
        href={resolve('/(app)/me/household')}
        onclick={(e) => choose(e, () => undefined)}
      >
        <span class="mark" aria-hidden="true"></span>
        <span class="title">{m['me.household']()}</span>
      </a>
    </div>
  </Popover>

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
  /* The brand's own pill, because it sits beside it and is the same kind of
     thing: a fixed point that says where you are. */
  .current {
    display: inline-flex;
    flex: 0 1 auto;
    align-items: center;
    gap: var(--space-1);
    min-width: 0;
    min-height: var(--control-sm);
    padding: var(--space-2) var(--space-3);
    border: 0;
    border-radius: var(--radius-full);
    background: var(--surface-nav-glass);
    backdrop-filter: blur(16px);
    color: var(--text);
    font: inherit;
    font-size: var(--text-sm);
    font-weight: var(--weight-medium);
    cursor: pointer;
    pointer-events: auto;
    transition: background-color var(--duration-fast) var(--ease-out);
  }

  .current:hover {
    background: var(--surface-selected);
  }

  .current:active {
    background: var(--surface-hover);
  }

  /* A long name gives way before the controls beside it do. */
  .name {
    min-width: 0;
    max-width: 14ch;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .current svg {
    flex: none;
    width: var(--space-4);
    height: var(--space-4);
    color: var(--text-muted);
  }

  .menu {
    display: flex;
    flex-direction: column;
    min-width: 14rem;
    max-width: 20rem;
  }

  .heading {
    padding: var(--space-2) var(--space-3) var(--space-1);
    color: var(--text-muted);
    font-size: var(--text-xs);
  }

  .item {
    display: flex;
    align-items: center;
    gap: var(--space-3);
    min-height: var(--control-sm);
    padding: var(--space-2) var(--space-3);
    border: none;
    border-radius: var(--radius-md);
    background: none;
    color: var(--text);
    font: inherit;
    font-size: var(--text-sm);
    text-align: start;
    text-decoration: none;
    cursor: pointer;
  }

  .item:hover {
    background: var(--surface-hover);
  }

  .mark {
    display: grid;
    flex: none;
    place-items: center;
    width: var(--space-4);
    height: var(--space-4);
    color: var(--accent);
  }

  .mark svg {
    width: 100%;
    height: 100%;
  }

  .label {
    display: flex;
    flex-direction: column;
    min-width: 0;
  }

  .title {
    overflow-wrap: anywhere;
  }

  .inherits {
    color: var(--text-muted);
    font-size: var(--text-xs);
  }

  .separator {
    margin: var(--space-1) var(--space-3);
    border: none;
    border-top: 1px solid var(--border);
  }
</style>
