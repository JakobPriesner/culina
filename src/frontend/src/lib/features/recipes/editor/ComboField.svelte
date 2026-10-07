<script lang="ts">
  import SuggestionList, { type Suggestion } from './SuggestionList.svelte';

  /**
   * A labelled text field offering a list without insisting on it: the unit/ingredient vocabulary is open.
   * Focus stays on the field; the highlighted option travels as `aria-activedescendant` so typing continues while the list is open.
   */
  interface Props {
    id: string;
    label: string;
    /** Names the popup for a screen reader that reaches it on its own. */
    listLabel: string;
    value: string;
    options: readonly Suggestion[];
    oninput: (value: string) => void;
    /** Enter, when the author has not picked a row. Adds the ingredient. */
    onsubmit?: () => void;
    /** Whether the cursor is here; only the focused field may query the shared suggestion store, or two would overwrite each other's answers. */
    focused?: boolean;
    inputmode?: 'text' | 'decimal';
    placeholder?: string;
  }

  let {
    id,
    label,
    listLabel,
    value,
    options,
    oninput,
    onsubmit,
    focused = $bindable(false),
    inputmode = 'text',
    placeholder
  }: Props = $props();

  let element = $state<HTMLInputElement>();
  /** Highlighted row, -1 for none: Enter means "what I typed" until a row is arrowed to, so an open vocabulary is not replaced by near-misses. */
  let highlighted = $state(-1);
  /** The text the list was dismissed at, so Escape holds until the next key. */
  let dismissed = $state<string | null>(null);

  const listId = $derived(`${id}-options`);
  const open = $derived(focused && options.length > 0 && value !== dismissed);

  function choose(index: number) {
    const chosen = options[index];

    if (!chosen) {
      return;
    }

    oninput(chosen.value);
    dismissed = chosen.value;
    highlighted = -1;
    element?.focus();
  }

  function onkeydown(event: KeyboardEvent) {
    if (event.key === 'Enter') {
      event.preventDefault();

      if (open && highlighted >= 0) {
        choose(highlighted);
      } else {
        onsubmit?.();
      }

      return;
    }

    if (!open) {
      return;
    }

    switch (event.key) {
      case 'ArrowDown':
        event.preventDefault();
        highlighted = highlighted >= options.length - 1 ? 0 : highlighted + 1;
        break;
      case 'ArrowUp':
        event.preventDefault();
        highlighted = highlighted <= 0 ? options.length - 1 : highlighted - 1;
        break;
      case 'Escape':
        // Stop the surrounding form or sheet also acting on this Escape.
        event.preventDefault();
        event.stopPropagation();
        dismissed = value;
        highlighted = -1;
        break;
    }
    // Tab is left alone: a list stealing it in a row of four fields would be worse than no list.
  }
</script>

<label class="field">
  <span class="label">{label}</span>

  <!-- The list is positioned against this, so it opens under its own field. -->
  <span class="anchor">
    <input
      bind:this={element}
      {id}
      class="ds-control"
      type="text"
      {inputmode}
      {placeholder}
      autocomplete="off"
      {value}
      role="combobox"
      aria-expanded={open}
      aria-controls={listId}
      aria-autocomplete="list"
      aria-activedescendant={open && highlighted >= 0 ? `${listId}-${highlighted}` : undefined}
      oninput={(event) => {
        highlighted = -1;
        dismissed = null;
        oninput(event.currentTarget.value);
      }}
      onfocus={() => (focused = true)}
      onblur={() => {
        focused = false;
        highlighted = -1;
      }}
      {onkeydown}
    />

    {#if open}
      <SuggestionList
        id={listId}
        label={listLabel}
        items={options}
        {highlighted}
        onchoose={choose}
      />
    {/if}
  </span>
</label>

<style>
  .field {
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
    min-width: 0;
  }

  .label {
    color: var(--text-subtle);
    font-size: var(--text-xs);
  }

  .anchor {
    position: relative;
    display: block;
  }
</style>
