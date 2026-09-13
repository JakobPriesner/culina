<script lang="ts">
  import { tick } from 'svelte';
  import { TextArea } from '$ds';

  import { m } from '$shell/i18n';
  import { insertMention, pendingMention, suggest, type PendingMention } from './mentions';
  import { formatQuantity } from '../formatQuantity';
  import { quantityLabels } from '../quantityLabels';
  import { scaleQuantity } from '../scaling';
  import { preferences } from '$shell/preferences.svelte';
  import type { Ingredient } from '../types';

  /**
   * A step, written the way a step is written, with `@` to point at an
   * ingredient.
   *
   * The picker opens on `@` and closes on its own as soon as what has been
   * typed names nothing — which is how "@olive oil in the pan" stops suggesting
   * after "oil" without needing a rule about where a name ends.
   *
   * Its last row offers to add the name that was typed, and that is the point
   * of the whole thing: someone writing the method can put an ingredient on the
   * list without leaving the sentence, and say how much later. Writing the
   * recipe builds the list instead of repeating it.
   */
  interface Props {
    id: string;
    label: string;
    value: string;
    ingredients: readonly Ingredient[];
    oninput: (value: string) => void;
    /** Puts a new ingredient on the list, under exactly this name. */
    onadd: (name: string) => void;
  }

  let { id, label, value, ingredients, oninput, onadd }: Props = $props();

  let field = $state<HTMLTextAreaElement>();
  let pending = $state<PendingMention | null>(null);
  let highlighted = $state(0);
  /**
   * Where a mention the author has already settled begins.
   *
   * Choosing a name or pressing Escape ends that mention. Without this the
   * cursor would still be sitting just after an `@`, and the picker would
   * reopen behind the very next keystroke — having just been told to go away.
   */
  let settled = $state<number | null>(null);

  const listId = $derived(`${id}-mentions`);
  const hintId = $derived(`${id}-hint`);
  const rowId = (index: number) => `${listId}-${index}`;

  const query = $derived(pending?.query.trim() ?? '');
  const matches = $derived(pending ? suggest(pending.query, ingredients) : []);

  /**
   * A name offered for the list, when there is one worth offering.
   *
   * Two words at most. A query may contain spaces, because "olive oil" is one
   * name — but "@olive oil into the pan" is a sentence the author kept writing,
   * and offering to add all of it as an ingredient would be absurd. Matching
   * names keep the picker open on their own for as long as they match; this is
   * only about what to do when nothing does.
   */
  const newName = $derived.by(() => {
    if (!query || query.split(/\s+/).length > 2) {
      return null;
    }

    return ingredients.some((one) => one.name.toLowerCase() === query.toLowerCase()) ? null : query;
  });

  const rows = $derived(matches.length + (newName ? 1 : 0));
  const open = $derived(pending !== null && pending.at !== settled && rows > 0);

  const amountOf = (one: Ingredient) =>
    formatQuantity(scaleQuantity(one.quantity, 1), preferences.locale, quantityLabels).text;

  function reconsider() {
    const next = field ? pendingMention(field.value, field.selectionStart) : null;

    // Only a different mention starts the highlight over. Arrowing down the
    // list moves the cursor nowhere, and a list that jumped back to its first
    // row on every key release could never be walked.
    if (next?.at !== pending?.at || next?.query !== pending?.query) {
      highlighted = 0;
    }

    if (next?.at !== settled) {
      settled = null;
    }

    pending = next;
  }

  /** Writes the name in, then hands the cursor back to the sentence. */
  async function choose(name: string) {
    if (!field || !pending) {
      return;
    }

    const written = insertMention(field.value, pending, name);

    settled = pending.at;
    pending = null;
    oninput(written.text);

    // The parent owns the value, so the cursor can only be placed once the new
    // text has actually reached the element.
    await tick();
    field.focus();
    field.setSelectionRange(written.caret, written.caret);
  }

  function pick(index: number) {
    const match = matches[index];
    // Past the end of the matches is the row that adds what was typed. Read
    // before anything else: putting the name on the list is what stops it from
    // being a new name, and `newName` is derived from that list.
    const name = match?.name ?? newName;

    if (!name) {
      return;
    }

    if (!match) {
      onadd(name);
    }

    void choose(name);
  }

  function onkeydown(event: KeyboardEvent) {
    if (!open) {
      return;
    }

    switch (event.key) {
      case 'ArrowDown':
        event.preventDefault();
        highlighted = (highlighted + 1) % rows;
        break;
      case 'ArrowUp':
        event.preventDefault();
        highlighted = (highlighted - 1 + rows) % rows;
        break;
      case 'Enter':
      case 'Tab':
        event.preventDefault();
        pick(highlighted);
        break;
      case 'Escape':
        // Stopped here, so the surrounding form or dialog does not also act on
        // it: dismissing the picker is the whole of what this Escape means.
        event.preventDefault();
        event.stopPropagation();
        settled = pending?.at ?? null;
        pending = null;
        break;
    }
  }

  /**
   * Listened for rather than passed down as four more props.
   *
   * Whether a mention is being typed depends on where the cursor is, and the
   * cursor moves for reasons a value-changed callback never hears about —
   * clicking into the middle of a word, arrowing back over one. The textarea
   * knows; the design-system component it lives in has no reason to.
   */
  $effect(() => {
    const element = field;

    if (!element) {
      return;
    }

    const close = () => {
      pending = null;
    };

    element.addEventListener('keydown', onkeydown);
    element.addEventListener('keyup', reconsider);
    element.addEventListener('click', reconsider);
    element.addEventListener('blur', close);

    return () => {
      element.removeEventListener('keydown', onkeydown);
      element.removeEventListener('keyup', reconsider);
      element.removeEventListener('click', reconsider);
      element.removeEventListener('blur', close);
    };
  });
</script>

<div class="field">
  <TextArea
    {id}
    {label}
    {value}
    bind:element={field}
    rows={2}
    describedBy={hintId}
    combobox={{
      expanded: open,
      controls: listId,
      active: open ? rowId(highlighted) : undefined
    }}
    oninput={(next) => {
      oninput(next);
      reconsider();
    }}
  />

  <!-- On the field, not floating above the form as an instruction nobody
       connects to anything. -->
  <p id={hintId} class="hint">{m['editor.mentionHint']()}</p>

  {#if open}
    <!-- The options are not buttons. Focus stays in the sentence and the
         highlight travels by aria-activedescendant, which is what lets someone
         keep typing; a focusable control inside an option would take the
         cursor out of the step and is not a thing an option may contain. The
         keyboard is handled on the textarea, which is where the keyboard is —
         hence the ignores below, which are about the pattern and not about a
         gap in it. -->
    <ul id={listId} class="picker" role="listbox" aria-label={m['editor.mentionListLabel']()}>
      {#each matches as match, index (match.id)}
        <!-- svelte-ignore a11y_click_events_have_key_events -->
        <li
          id={rowId(index)}
          role="option"
          aria-selected={index === highlighted}
          class:highlighted={index === highlighted}
          onmousedown={(event) => event.preventDefault()}
          onclick={() => pick(index)}
        >
          <span>{match.name}</span>
          <span class="amount">{amountOf(match)}</span>
        </li>
      {/each}

      {#if newName}
        <!-- svelte-ignore a11y_click_events_have_key_events -->
        <li
          id={rowId(matches.length)}
          role="option"
          aria-selected={highlighted === matches.length}
          class:highlighted={highlighted === matches.length}
          onmousedown={(event) => event.preventDefault()}
          onclick={() => pick(matches.length)}
        >
          <span>{m['editor.mentionAdd']({ name: newName })}</span>
        </li>
      {/if}
    </ul>
  {/if}
</div>

<style>
  .field {
    position: relative;
  }

  .hint {
    margin-top: var(--space-1);
    color: var(--text-subtle);
    font-size: var(--text-xs);
  }

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

  .picker li {
    display: flex;
    align-items: baseline;
    justify-content: space-between;
    gap: var(--space-4);
    padding: var(--space-2) var(--space-3);
    border-radius: var(--radius-sm);
    cursor: pointer;
  }

  .picker li.highlighted,
  .picker li:hover {
    background: var(--surface-selected);
  }

  .amount {
    color: var(--text-muted);
    font-size: var(--text-sm);
    font-variant-numeric: tabular-nums;
    white-space: nowrap;
  }
</style>
