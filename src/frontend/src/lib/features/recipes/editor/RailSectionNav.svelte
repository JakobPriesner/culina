<script lang="ts">
  /**
   * The links to a form's sections, with the one being read marked.
   *
   * Only on a wide screen: five pills above a form on a 360px screen cost more
   * room than the scrolling they save.
   */
  interface Props {
    /** Names the nav, which is a list of links without a heading. */
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

    <!-- A fragment on the page that is already open, which the rule cannot
         tell apart from a route. `resolve()` is for routes, and there is
         nothing here to resolve. -->
    <!-- eslint-disable-next-line svelte/no-navigation-without-resolve -->
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

  /*
   * Wide enough for a column of its own: the bar unrolls into the rail the
   * settings screens already use, so an inner navigation looks like this app's
   * inner navigation wherever it appears.
   */
  @media (min-width: 64rem) {
    .sections {
      display: flex;
      flex-direction: column;
      gap: var(--space-1);
      margin-top: var(--space-2);
    }

    /* The same pill the settings rail and the navbar use. */
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
