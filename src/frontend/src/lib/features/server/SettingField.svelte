<script lang="ts">
  import { Field, TextInput } from '$ds';
  import { m } from '$shell/i18n';

  /**
   * One text setting; one pinned by the environment is shown read-only with the variable named, as
   * the environment wins on restart.
   */
  interface Props {
    label: string;
    value: string;
    variable: string;
    pinned?: boolean;
    hint?: string;
    error?: string;
    placeholder?: string;
    type?: 'text' | 'password' | 'url';
    inputmode?: 'text' | 'numeric' | 'url';
    autocomplete?: HTMLInputElement['autocomplete'];
    disabled?: boolean;
  }

  let {
    label,
    value = $bindable(),
    variable,
    pinned = false,
    hint,
    error,
    placeholder,
    type = 'text',
    inputmode,
    autocomplete = 'off',
    disabled = false
  }: Props = $props();
</script>

<Field {label} hint={pinned ? m['server.pinned']({ variable }) : hint} {error}>
  {#snippet children({ id, describedBy, invalid })}
    <TextInput
      {id}
      {describedBy}
      {invalid}
      {type}
      revealLabel={m['field.showPassword']()}
      {inputmode}
      {autocomplete}
      {placeholder}
      disabled={disabled || pinned}
      {value}
      oninput={(typed) => (value = typed)}
    />
  {/snippet}
</Field>
