<script lang="ts">
  import { m } from '$shell/i18n';
  import { session } from './session.svelte';

  /**
   * Which kitchen is on screen and the way to another, as rows for the account menu; shown even
   * with one household for "New household". The menu owns the sheet that creates one, since a
   * dialog inside the closing panel would close with it.
   */
  interface Props {
    onswitch: () => void;
    oncreate: () => void;
  }

  let { onswitch, oncreate }: Props = $props();

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

  <button type="button" class="item" onclick={oncreate}>
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
{/if}

<style>
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
