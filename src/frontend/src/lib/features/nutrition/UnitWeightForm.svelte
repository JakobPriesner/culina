<script lang="ts">
  import { untrack } from 'svelte';

  import { Button, Field, TextInput } from '$ds';
  import { m } from '$shell/i18n';

  import { gramsField, parseGrams } from './weights';

  /**
   * One number in grams, with save and, when there is something to forget, a way back. Used where a household
   * says what one unit of an ingredient weighs: in the correction sheet and in the household's settings.
   * Nothing is sent before the number is one the server takes, so a slip is told here and not by a failed save.
   */
  interface Props {
    label: string;
    /** What the field starts with; empty when nobody has said. */
    initial: number | null;
    /** What a typical weight would be, and from where: quiet words under the field. */
    hint?: string;
    /** Names the button when the form sits beside others of its kind. */
    saveLabel: string;
    removeLabel?: string;
    onsave: (grams: number) => void;
    onremove?: () => void;
  }

  let { label, initial, hint, saveLabel, removeLabel, onsave, onremove }: Props = $props();

  let typed = $state(untrack(() => (initial === null ? '' : gramsField(initial))));
  let invalid = $state(false);

  function submit(event: SubmitEvent) {
    event.preventDefault();

    const grams = parseGrams(typed);

    if (grams === null) {
      invalid = true;

      return;
    }

    onsave(grams);
  }
</script>

<form class="form" onsubmit={submit} novalidate>
  <Field {label} {hint} error={invalid ? m['nutrition.weight.invalid']() : undefined}>
    {#snippet children({ id, describedBy, invalid: bad })}
      <TextInput
        {id}
        {describedBy}
        invalid={bad}
        bind:value={typed}
        inputmode="decimal"
        autocomplete="off"
        oninput={() => (invalid = false)}
      />
    {/snippet}
  </Field>

  <div class="actions">
    <Button type="submit" variant="primary">{saveLabel}</Button>
    {#if removeLabel && onremove}
      <Button onclick={onremove}>{removeLabel}</Button>
    {/if}
  </div>
</form>

<style>
  .form {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
    min-width: 0;
  }

  .actions {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2);
  }
</style>
