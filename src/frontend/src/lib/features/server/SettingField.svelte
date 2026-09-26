<script lang="ts">
  import { Field, TextInput } from '$ds';
  import { m } from '$shell/i18n';

  /**
   * One text setting, and what the deployment says about it.
   *
   * A setting the environment pins is shown and not editable, with the
   * variable that pins it named where the hint would be. Editing it would do
   * nothing — the environment wins on the next start — and a field that
   * accepts an edit and then ignores it is worse than one that says why it
   * cannot.
   */
  interface Props {
    label: string;
    value: string;
    /** The environment variable that sets this, e.g. `Database__Host`. */
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
