<script lang="ts">
  /** Section links with the current one marked; wide screens only, where it costs no room above the form. */
  interface Props {
    /** Names the nav, which has no heading. */
    label: string;
    sections: readonly { id: string; label: string; count?: number }[];
    current: string | null;
    onjump: (event: MouseEvent, id: string) => void;
  }

  let { label, sections, current, onjump }: Props = $props();
</script>

<nav class="sections" aria-label={label}>
  {#each sections as section (section.id)}
    {@const selected = current === section.id}

    <!-- eslint-disable-next-line svelte/no-navigation-without-resolve -- In-page fragment, not a route. -->
    <a
      class="section"
      class:selected
      href="#{section.id}"
      aria-current={selected ? 'true' : undefined}
      onclick={(event) => onjump(event, section.id)}
    >
      <span class="label">{section.label}</span>
      {#if section.count !== undefined}
        <span class="count">{section.count}</span>
      {/if}
    </a>
  {/each}
</nav>

<style>
  .sections {
    display: none;
  }

  @media (min-width: 64rem) {
    .sections {
      display: flex;
      flex-direction: column;
      gap: var(--space-1);
      margin-top: var(--space-2);
    }

    .section {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: var(--space-3);
      min-height: var(--control-sm);
      padding-inline: var(--space-4);
      border-radius: var(--radius-full);
      color: var(--text-muted);
      font-size: var(--text-sm);
      font-weight: var(--weight-medium);
      text-decoration: none;
      transition:
        color var(--duration-fast) var(--ease-out),
        background-color var(--duration-fast) var(--ease-out);
    }

    .section:hover {
      background: var(--surface-hover);
      color: var(--text);
    }

    .section.selected {
      background: var(--surface-accent-subtle);
      color: var(--accent);
      font-weight: var(--weight-semibold);
    }

    .count {
      color: var(--text-subtle);
      font-variant-numeric: tabular-nums;
    }

    .section.selected .count {
      color: inherit;
    }
  }
</style>
