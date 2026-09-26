<script lang="ts">
  import { Field, TextInput } from '$ds';
  import { m } from '$shell/i18n';

  import type { Submission } from './submission.svelte';

  /**
   * One labelled text field, wired to a submission.
   *
   * Exists so the three auth pages do not each remember to pass the error down,
   * to register the field's id for focus, and to mark it invalid. A field error
   * belongs on the field — never in a toast, where it is separated from the
   * thing it is about.
   */
  interface Props {
    /** The name the server uses for this field in its problem document. */
    name: string;
    label: string;
    value: string;
    submission: Submission;
    type?: 'text' | 'email' | 'password';
    autocomplete?: HTMLInputElement['autocomplete'];
    inputmode?: 'text' | 'email';
    hint?: string;
    required?: boolean;
    autofocus?: boolean;
  }

  let {
    name,
    label,
    value = $bindable(),
    submission,
    type = 'text',
    autocomplete,
    inputmode,
    hint,
    required = true,
    autofocus = false
  }: Props = $props();

  const error = $derived(submission.errorFor(name));

  // Made here rather than inside Field, because the submission has to be able
  // to find this control to move focus to it before it has been rendered.
  const id = $props.id();

  let element = $state<HTMLInputElement>();

  $effect(() => {
    // In an effect so a renamed field re-registers rather than leaving the
    // submission pointing at a control that no longer answers to that name.
    submission.register(name, id);
  });

  $effect(() => {
    if (autofocus) {
      element?.focus();
    }
  });
</script>

<Field {id} {label} {hint} {error} {required}>
  {#snippet children({ describedBy, invalid })}
    <TextInput
      {id}
      {describedBy}
      {invalid}
      {type}
      revealLabel={m['field.showPassword']()}
      {autocomplete}
      {inputmode}
      {required}
      bind:value
      bind:element
    />
  {/snippet}
</Field>
