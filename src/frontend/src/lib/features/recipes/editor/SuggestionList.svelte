<script lang="ts">
  /**
   * The list a field offers while somebody is typing into it.
   *
   * Presentation and pointer handling only. The keyboard belongs to the field,
   * because the cursor never leaves it: the options are not focusable, the
   * field keeps focus, and which row is highlighted travels as
   * `aria-activedescendant`. That is the combobox pattern, and it is the only
   * arrangement in which someone can keep typing while a list is open.
   *
   * Two fields use it — mentioning an ingredient inside a step, and naming one
   * on a line — which is why it is a component rather than markup written
   * twice with one of the copies eventually forgetting an aria attribute.
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
  }

  let { id, label, items, highlighted, onchoose }: Props = $props();
</script>

<ul {id} class="picker" role="listbox" aria-label={label}>
  {#each items as item, index (item.value)}
    <!-- The keyboard is handled on the field, which is where the keyboard is,
         so these ignores are about the pattern rather than a gap in it. -->
    <!-- svelte-ignore a11y_click_events_have_key_events -->
    <li
      id="{id}-{index}"
      role="option"
      aria-selected={index === highlighted}
      class:highlighted={index === highlighted}
      onmousedown={(event) => event.preventDefault()}
      onclick={() => onchoose(index)}
    >
      <span class="label">{item.label}</span>
      {#if item.detail}<span class="detail">{item.detail}</span>{/if}
    </li>
  {/each}
</ul>

<style>
  /*
   * Below the field rather than beside the cursor. A popover that follows the
   * caret is charming on a desktop and unusable on a phone, where it lands
   * under the keyboard about half the time.
   */
  .picker {
    position: absolute;
    z-index: 3;
    inset-inline: 0;
    top: calc(100% - var(--space-1));
    max-height: 14rem;
    overflow-y: auto;
    margin: 0;
    padding: var(--space-1);
    list-style: none;
    background: var(--surface-overlay);
    border: 1px solid var(--border);
    border-radius: var(--radius-md);
    box-shadow: var(--shadow-overlay);
  }

  li {
    display: flex;
    align-items: baseline;
    justify-content: space-between;
    gap: var(--space-4);
    padding: var(--space-2) var(--space-3);
    border-radius: var(--radius-sm);
    cursor: pointer;
  }

  li.highlighted,
  li:hover {
    background: var(--surface-selected);
  }

  .label {
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .detail {
    color: var(--text-muted);
    font-size: var(--text-sm);
    font-variant-numeric: tabular-nums;
    white-space: nowrap;
  }
</style>
