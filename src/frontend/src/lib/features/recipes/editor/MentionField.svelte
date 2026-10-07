<script lang="ts">
  import { tick } from 'svelte';
  import { TextArea } from '$ds';

  import { m } from '$shell/i18n';
  import SuggestionList, { type Suggestion } from './SuggestionList.svelte';
  import {
    insertMention,
    pendingMention,
    suggest,
    toSegments,
    writesOn,
    type PendingMention
  } from './mentions';
  import { formatQuantity } from '../formatQuantity';
  import { quantityLabels } from '../quantityLabels';
  import { scaleQuantity } from '../scaling';
  import { preferences } from '$shell/preferences.svelte';
  import type { Ingredient } from '../types';

  /** A step textarea where `@` opens an ingredient picker; its last row adds the typed name to the list. */
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
  /** Start of a mention already settled (chosen or Escaped), so the picker doesn't reopen behind the next keystroke. */
  let settled = $state<number | null>(null);

  const listId = $derived(`${id}-mentions`);
  const hintId = $derived(`${id}-hint`);
  const rowId = (index: number) => `${listId}-${index}`;

  const query = $derived(pending?.query.trim() ?? '');
  const matches = $derived(pending ? suggest(pending.query, ingredients) : []);
  const segments = $derived(toSegments(value, ingredients));

  /** A name to offer for the list: at most two words ("@olive oil into the pan" is a sentence), and not one already present. */
  const newName = $derived.by(() => {
    if (!query || query.split(/\s+/).length > 2 || writesOn(query, ingredients)) {
      return null;
    }

    return ingredients.some((one) => one.name.toLowerCase() === query.toLowerCase()) ? null : query;
  });

  const options = $derived<readonly Suggestion[]>([
    ...matches.map((match) => ({
      value: match.id,
      label: match.name,
      detail: amountOf(match)
    })),
    ...(newName
      ? [{ value: `add:${newName}`, label: m['editor.mentionAdd']({ name: newName }) }]
      : [])
  ]);

  const rows = $derived(options.length);
  const open = $derived(pending !== null && pending.at !== settled && rows > 0);

  const amountOf = (one: Ingredient) =>
    formatQuantity(scaleQuantity(one.quantity, 1), preferences.locale, quantityLabels).text;

  function reconsider() {
    const next = field ? pendingMention(field.value, field.selectionStart) : null;

    // Only a different mention resets the highlight; arrowing moves no cursor and must stay walkable.
    if (next?.at !== pending?.at || next?.query !== pending?.query) {
      highlighted = 0;
    }

    if (next?.at !== settled) {
      settled = null;
    }

    pending = next;
  }

  async function choose(name: string) {
    if (!field || !pending) {
      return;
    }

    const written = insertMention(field.value, pending, name);

    settled = pending.at;
    pending = null;
    oninput(written.text);

    // The parent owns the value: place the cursor only after the new text reaches the element.
    await tick();
    field.focus();
    field.setSelectionRange(written.caret, written.caret);
  }

  function pick(index: number) {
    const match = matches[index];
    // Past the matches is the add row. Read first: adding the name makes `newName` (derived from the list) null.
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
        // Stopped so the surrounding form or dialog does not also act on it.
        event.preventDefault();
        event.stopPropagation();
        settled = pending?.at ?? null;
        pending = null;
        break;
    }
  }

  /** Listened for directly: the cursor moves (clicks, arrowing) without any value callback. */
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
  <div class="editor-wrap">
    <div class="backdrop" aria-hidden="true">
      {#each segments as segment, i (i)}{#if segment.kind === 'ingredient'}<mark
            class="mention-pill">@{segment.name}</mark
          >{:else}<span>{segment.text}</span>{/if}{/each}{#if value.endsWith('\n')}<span
          >&nbsp;</span
        >{/if}
    </div>

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
  </div>

  <p id={hintId} class="hint">{m['editor.mentionHint']()}</p>

  {#if open}
    <SuggestionList
      id={listId}
      label={m['editor.mentionListLabel']()}
      items={options}
      {highlighted}
      {query}
      onchoose={pick}
    />
  {/if}
</div>

<style>
  .field {
    position: relative;
  }

  .editor-wrap {
    position: relative;
    width: 100%;
  }

  /* Native textarea transparent so the backdrop shows through. */
  .editor-wrap :global(.ds-control) {
    position: relative;
    z-index: 1;
    background: transparent;
  }

  /* Mirrors .ds-control box, font, padding and line height 1:1 so text lines up. */
  .backdrop {
    position: absolute;
    inset: 0;
    z-index: 0;
    margin: 0;
    padding-inline: var(--space-4);
    padding-block: var(--space-2);
    border: 1px solid transparent;
    border-radius: var(--radius-md);
    background: var(--surface-raised);
    color: transparent;
    font: inherit;
    line-height: var(--leading-normal);
    white-space: pre-wrap;
    word-break: break-word;
    overflow-wrap: break-word;
    overflow: hidden;
    pointer-events: none;
    user-select: none;
  }

  .mention-pill {
    color: transparent;
    background: var(--surface-accent-subtle);
    border-radius: var(--radius-sm);
    box-shadow: 0 0 0 1px var(--border-accent);
  }

  .hint {
    margin-top: var(--space-1);
    color: var(--text-subtle);
    font-size: var(--text-xs);
  }
</style>
