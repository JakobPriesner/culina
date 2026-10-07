<script lang="ts">
  import { Field, TextInput } from '$ds';
  import { m } from '$shell/i18n';

  import type { Submission } from './submission.svelte';

  /** A labelled text field wired to a submission, so auth pages need not pass the error down or register the id; a field error belongs on the field, never in a toast. */
  interface Props {
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

  // Made here, not in Field: the submission must find this control to focus it before it renders.
  const id = $props.id();

  let element = $state<HTMLInputElement>();

  $effect(() => {
    // In an effect so a renamed field re-registers instead of leaving the submission with a stale name.
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
