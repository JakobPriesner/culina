<script lang="ts">
  import type { Snippet } from 'svelte';

  /**
   * Label, hint, error, and the wiring between them.
   *
   * Every control is wrapped in this rather than each one repeating
   * `aria-describedby` and `aria-invalid`. Getting it right by construction is
   * the only way it stays right: the version that is written by hand per form
   * is the version that is wrong on the third form.
   */
  interface Props {
    /** Receives the ids the control must carry. */
    children: Snippet<[{ id: string; describedBy: string | undefined; invalid: boolean }]>;
    label: string;
    /** Shown before anything is wrong: units, format, why it is asked for. */
    hint?: string;
    /** Replaces the hint when present, because two messages compete. */
    error?: string;
    /** Marks the control, and says so in words rather than with an asterisk. */
    required?: boolean;
    optionalText?: string;
    /** Turns the label into a group caption, for radios and checkbox sets. */
    group?: boolean;
  }

  let {
    children,
    label,
    hint,
    error,
    required = false,
    optionalText,
    group = false
  }: Props = $props();

  const id = $props.id();
  const hintId = `${id}-hint`;
  const errorId = `${id}-error`;

  const describedBy = $derived(error ? errorId : hint ? hintId : undefined);
</script>

<svelte:element this={group ? 'fieldset' : 'div'} class="field" role={group ? 'group' : undefined}>
  <svelte:element this={group ? 'legend' : 'label'} class="label" for={group ? undefined : id}>
    {label}
    {#if !required && optionalText}
      <span class="optional">{optionalText}</span>
    {/if}
  </svelte:element>

  {@render children({ id, describedBy, invalid: Boolean(error) })}

  {#if error}
    <!-- Assertive would interrupt mid-typing; a form error can wait for a pause. -->
    <p class="message error" id={errorId} role="alert">{error}</p>
  {:else if hint}
    <p class="message hint" id={hintId}>{hint}</p>
  {/if}
</svelte:element>

<style>
  .field {
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
    min-width: 0;
    margin: 0;
    padding: 0;
    border: none;
  }

  .label {
    display: flex;
    align-items: baseline;
    gap: var(--space-2);
    padding: 0;
    color: var(--text);
    font-size: var(--text-sm);
    font-weight: var(--weight-medium);
  }

  .optional {
    color: var(--text-subtle);
    font-weight: var(--weight-regular);
  }

  .message {
    font-size: var(--text-sm);
  }

  .hint {
    color: var(--text-muted);
  }

  .error {
    color: var(--text-danger);
  }
</style>
