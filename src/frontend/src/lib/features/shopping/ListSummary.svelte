<script lang="ts">
  import { resolve } from '$app/paths';
  import { Chevron } from '$ds';

  import { m } from '$shell/i18n';
  import { woche } from '$shell/woche.svelte';

  import { nameOf } from './sections';
  import { shopping } from './stores/shopping.svelte';

  /**
   * The list at a glance under the plan, where the list is not beside it: what is still to buy, a line per section in
   * shop order with its names, and the list one tap away. A glance, not a second list: nothing here is ticked.
   */
  const shown = 4;

  const sections = $derived(
    shopping.toBuy.map((group) => ({
      name: nameOf(group.section),
      count: group.items.length,
      // Lines kept apart by their units are one name here.
      names: [...new Set(group.items.map((item) => item.name))].join(', ')
    }))
  );
</script>

{#if shopping.remaining > 0}
  <section class="summary">
    <div class="head">
      <h2>
        <!-- The heading is the link, stretched over the summary. -->
        <a
          href={resolve('/(app)/week/shopping')}
          onclick={() => woche.leave('plan', 'list', window.scrollY)}>{m['shopping.title']()}</a
        >
      </h2>
      <span class="count">{m['shopping.remaining']({ count: shopping.remaining })}</span>
      <Chevron />
    </div>
    <ul>
      {#each sections.slice(0, shown) as section (section.name)}
        <li>
          <span class="section">{section.name} <span class="number">{section.count}</span></span>
          <span class="names">{section.names}</span>
        </li>
      {/each}
    </ul>
    {#if sections.length > shown}
      <p class="more">{m['shopping.summary.more']({ count: sections.length - shown })}</p>
    {/if}
  </section>
{/if}

<style>
  .summary {
    position: relative;
    margin-top: var(--space-6);
    padding: var(--space-3) var(--space-4);
    border: 1px solid var(--border);
    border-radius: var(--radius-md);
    background: var(--surface-raised);
  }

  .head {
    display: flex;
    align-items: baseline;
    gap: var(--space-2);
  }

  h2 {
    font-size: var(--text-base);
    font-weight: var(--weight-semibold);
  }

  a {
    color: inherit;
    text-decoration: none;
  }

  a::after {
    content: '';
    position: absolute;
    inset: 0;
    border-radius: inherit;
  }

  a:focus-visible {
    outline: none;
  }

  a:focus-visible::after {
    outline: 2px solid var(--border-focus);
    outline-offset: 2px;
  }

  .head > :global(svg) {
    align-self: center;
  }

  .count {
    flex: 1;
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  ul {
    margin: var(--space-2) 0 0;
    padding: 0;
    list-style: none;
  }

  /* One line a section: its name whole, its names cut short where the line ends. */
  li {
    display: flex;
    gap: var(--space-2);
    font-size: var(--text-sm);
    line-height: 1.6;
  }

  .section {
    flex: none;
    font-weight: var(--weight-semibold);
  }

  .number {
    color: var(--text-muted);
    font-weight: var(--weight-regular);
  }

  .names {
    min-width: 0;
    overflow: hidden;
    color: var(--text-muted);
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .more {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }
</style>
