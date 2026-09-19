<script lang="ts">
  import { m } from '$shell/i18n';

  /**
   * The list a field offers while somebody is typing into it.
   *
   * Presentation and pointer handling only. The keyboard belongs to the field,
   * because the cursor never leaves it: the options are not focusable, the
   * field keeps focus, and which row is highlighted travels as
   * `aria-activedescendant`. That is the combobox pattern, and it is the only
   * arrangement in which someone can keep typing while a list is open.
   */
  export interface Suggestion {
    /** Distinguishes rows, and what choosing it returns. */
    readonly value: string;
    /** What is read out and shown. */
    readonly label: string;
    /** Shown quietly on the right: an amount, a shop section. */
    readonly detail?: string;
  }

  interface Props {
    id: string;
    /** Names the list for a screen reader reaching it on its own. */
    label: string;
    items: readonly Suggestion[];
    highlighted: number;
    onchoose: (index: number) => void;
    query?: string;
  }

  let { id, label, items, highlighted, onchoose, query = '' }: Props = $props();
</script>

<ul {id} class="picker" role="listbox" aria-label={label}>
  {#each items as item, index (item.value)}
    {@const isAdd = item.value.startsWith('add:')}
    <!-- The keyboard is handled on the field, which is where the keyboard is,
         so these ignores are about the pattern rather than a gap in it. -->
    <!-- svelte-ignore a11y_click_events_have_key_events -->
    <li
      id="{id}-{index}"
      role="option"
      aria-label={item.label}
      aria-selected={index === highlighted}
      class:highlighted={index === highlighted}
      class:is-add={isAdd}
      onmousedown={(event) => event.preventDefault()}
      onclick={() => onchoose(index)}
    >
      <span class="row-content">
        {#if isAdd}
          <span class="icon add-icon" aria-hidden="true">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5">
              <path d="M12 5v14M5 12h14" stroke-linecap="round" />
            </svg>
          </span>
          <span class="label add-label">{item.label}</span>
        {:else}
          <span class="icon glyph-icon" aria-hidden="true">@</span>
          <span class="label">
            {#if query && item.label.toLowerCase().includes(query.toLowerCase())}
              {@const q = query.toLowerCase()}
              {@const matchIdx = item.label.toLowerCase().indexOf(q)}
              {item.label.slice(0, matchIdx)}<mark class="match"
                >{item.label.slice(matchIdx, matchIdx + q.length)}</mark
              >{item.label.slice(matchIdx + q.length)}
            {:else}
              {item.label}
            {/if}
          </span>
        {/if}
      </span>

      {#if item.detail}
        <span class="detail">{item.detail}</span>
      {/if}
    </li>
  {/each}

  <li class="nav-hint" aria-hidden="true">
    {m['editor.mentionNavHint']()}
  </li>
</ul>

<style>
  /*
   * Apple-style floating suggestion palette with frosted glass material.
   */
  .picker {
    position: absolute;
    z-index: var(--z-overlay, 100);
    inset-inline: 0;
    top: calc(100% + var(--space-1));
    max-height: 16rem;
    overflow-y: auto;
    scrollbar-gutter: stable;
    overscroll-behavior: contain;
    margin: 0;
    padding: var(--space-1);
    list-style: none;
    background: color-mix(in srgb, var(--surface-overlay) 92%, transparent);
    backdrop-filter: blur(24px) saturate(180%);
    -webkit-backdrop-filter: blur(24px) saturate(180%);
    border: 1px solid var(--border);
    border-radius: var(--radius-lg);
    box-shadow: var(--shadow-overlay);
    animation: paletteIn var(--duration-fast) var(--ease-out);
  }

  @keyframes paletteIn {
    from {
      opacity: 0;
      transform: translateY(-2px) scale(0.99);
    }
    to {
      opacity: 1;
      transform: translateY(0) scale(1);
    }
  }

  li[role='option'] {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-3);
    padding: var(--space-2) var(--space-3);
    border-radius: var(--radius-md);
    cursor: pointer;
    font-size: var(--text-sm);
    transition: background-color var(--duration-fast) var(--ease-out);
  }

  li[role='option'].highlighted {
    background: var(--surface-selected);
  }

  li[role='option']:hover:not(.highlighted) {
    background: var(--surface-hover);
  }

  .row-content {
    display: flex;
    align-items: center;
    gap: var(--space-2);
    min-width: 0;
    overflow: hidden;
  }

  .icon {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    flex: none;
  }

  .glyph-icon {
    width: 1.25rem;
    height: 1.25rem;
    border-radius: var(--radius-full);
    background: color-mix(in srgb, var(--accent) 12%, transparent);
    color: var(--accent);
    font-size: 0.75rem;
    font-weight: var(--weight-bold);
  }

  .add-icon {
    width: 1.25rem;
    height: 1.25rem;
    border-radius: var(--radius-full);
    background: var(--accent);
    color: var(--accent-contrast);
  }

  .add-icon svg {
    width: 0.75rem;
    height: 0.75rem;
  }

  .label {
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    color: var(--text);
  }

  .add-label {
    font-weight: var(--weight-medium);
    color: var(--accent);
  }

  .match {
    background: transparent;
    color: var(--accent);
    font-weight: var(--weight-bold);
  }

  .detail {
    flex: none;
    padding: 0.1em 0.45em;
    border-radius: var(--radius-sm);
    background: var(--surface-sunken);
    color: var(--text-muted);
    font-size: var(--text-xs);
    font-variant-numeric: tabular-nums;
    white-space: nowrap;
  }

  .nav-hint {
    padding: var(--space-2) var(--space-3) var(--space-1);
    border-top: 1px solid var(--border);
    margin-top: var(--space-1);
    font-size: 0.6875rem;
    color: var(--text-subtle);
    text-align: center;
  }
</style>
