<script lang="ts">
  import { resolve } from '$app/paths';
  import { ActionMenu } from '$ds';

  import { m } from '$shell/i18n';
  import NewHouseholdSheet from './NewHouseholdSheet.svelte';
  import { session } from './session.svelte';

  /**
   * Which kitchen is on screen, and the way to another one.
   *
   * In the header's glass capsule beside search, because both are about what
   * is on screen: find something in this kitchen, or look at another one. It
   * is there with a single
   * household too, because "New household" has to live somewhere a person
   * would look for it.
   *
   * A house rather than the name: the header has no room for a name, and one
   * cut down to "Cl's…" says less than an icon does. The name is in its label
   * and tooltip, and ticked at the top of the menu it opens.
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

  function select(householdId: string) {
    if (householdId === session.activeHouseholdId) {
      return;
    }

    session.selectHousehold(householdId);
    onswitch();
  }
</script>

{#if active}
  <ActionMenu minWidth="14rem" maxWidth="20rem" wrap>
    {#snippet trigger({ popovertarget })}
      <button
        type="button"
        class="current"
        {popovertarget}
        aria-label={m['household.switch.label']({ name: active.name })}
        title={active.name}
      >
        <svg
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="1.8"
          stroke-linecap="round"
          stroke-linejoin="round"
          aria-hidden="true"
        >
          <path d="M4 10.5 12 4l8 6.5" />
          <path d="M6 9v10h12V9" />
          <path d="M10 19v-5h4v5" />
        </svg>
      </button>
    {/snippet}

    <p class="heading">{m['household.switch.heading']()}</p>

    {#each session.households as household (household.householdId)}
      {@const current = household.householdId === active.householdId}
      <button
        type="button"
        class="item"
        aria-current={current || undefined}
        onclick={() => select(household.householdId)}
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

    <button type="button" class="item" onclick={() => (creating = true)}>
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

    <a class="item" href={resolve('/(app)/me/household')}>
      <span class="mark" aria-hidden="true"></span>
      <span class="title">{m['me.household']()}</span>
    </a>
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
  /* Drawn like the search button it shares the header's capsule with: the
     capsule is the glass, and this is only a lit circle under the pointer. */
  .current {
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
    pointer-events: auto;
    transition: background-color var(--duration-fast) var(--ease-out);
  }

  .current:hover {
    background: var(--surface-selected);
  }

  .current:active {
    background: var(--surface-hover);
  }

  .current svg {
    width: calc(var(--space-4) + var(--space-1));
    height: calc(var(--space-4) + var(--space-1));
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
</style>
