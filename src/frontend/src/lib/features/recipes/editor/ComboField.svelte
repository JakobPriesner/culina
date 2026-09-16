<script lang="ts">
  import SuggestionList, { type Suggestion } from './SuggestionList.svelte';

  /**
   * A labelled text field that offers a list, without ever insisting on it.
   *
   * Two of the ingredient fields need this and they need it for the same
   * reason: the vocabulary is open. A unit nobody has written before is added
   * by writing it, and an ingredient is whatever somebody types — so the list
   * is help, never a gate. Everything here follows from that.
   *
   * The keyboard stays on the field. Options are not focusable and which one
   * is highlighted travels as `aria-activedescendant`, which is the combobox
   * pattern and the only arrangement in which someone can keep typing while a
   * list is open.
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
    /**
     * Whether the cursor is here, readable by the parent.
     *
     * The ingredient suggestions come from one shared store, so only the field
     * somebody is actually typing in may ask it a question. Without this, two
     * of these on screen at once would take turns overwriting each other's
     * answers.
     */
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
  /**
   * Which row is highlighted, or -1 for none — and -1 is the one that matters.
   *
   * Nothing is highlighted until somebody arrows into the list on purpose, so
   * Enter means "this is what I typed" by default and only means "the row I
   * chose" once a row has been chosen. A list that pre-selected its first row
   * would quietly replace invented words with near-misses, which is exactly
   * what an open vocabulary must not do.
   */
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
        // Stopped here so the surrounding form or sheet does not also act on
        // it: dismissing the list is the whole of what this Escape means.
        event.preventDefault();
        event.stopPropagation();
        dismissed = value;
        highlighted = -1;
        break;
    }
    // Tab is left alone. It moves to the next field, which is what Tab does —
    // and in a row of four fields, a list quietly stealing it would be worse
    // than no list at all.
  }
</script>

<label class="field">
  <span class="label">{label}</span>

  <!-- The list is positioned against this, not the row, so it opens under the
       field it belongs to. -->
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
