<script lang="ts">
  import { Switch } from '$ds';
  import { m } from '$shell/i18n';

  import SettingField from './SettingField.svelte';
  import { databaseVariable, wholeNumber, type DatabaseDraft, type DatabaseFacts } from './types';

  /**
   * How to reach PostgreSQL.
   *
   * The password is write-only, like an API key: no response carries it, so
   * the box starts empty and empty means "keep the one that is set". Rendered
   * as a box that looks unset, it would read as "no password" — and saving
   * would look like it had cleared one.
   */
  interface Props {
    draft: DatabaseDraft;
    facts: DatabaseFacts;
    disabled?: boolean;
  }

  let { draft = $bindable(), facts, disabled = false }: Props = $props();

  const pinned = (field: keyof DatabaseDraft) => facts.pinned.has(databaseVariable(field));

  const problem = (text: string) =>
    wholeNumber(text) === null ? m['server.number.invalid']() : undefined;
</script>

<div class="pair">
  <SettingField
    label={m['server.database.host']()}
    variable={databaseVariable('host')}
    pinned={pinned('host')}
    placeholder="db"
    {disabled}
    bind:value={draft.host}
  />

  <SettingField
    label={m['server.database.port']()}
    variable={databaseVariable('port')}
    pinned={pinned('port')}
    inputmode="numeric"
    error={problem(draft.port)}
    {disabled}
    bind:value={draft.port}
  />
</div>

<SettingField
  label={m['server.database.name']()}
  variable={databaseVariable('name')}
  pinned={pinned('name')}
  placeholder="culina"
  {disabled}
  bind:value={draft.name}
/>

<SettingField
  label={m['server.database.username']()}
  hint={m['server.database.username.hint']()}
  variable={databaseVariable('username')}
  pinned={pinned('username')}
  placeholder="culina_app"
  autocomplete="username"
  {disabled}
  bind:value={draft.username}
/>

<SettingField
  label={m['server.database.password']()}
  hint={facts.passwordConfigured ? m['server.database.password.keep']() : undefined}
  variable={databaseVariable('password')}
  pinned={pinned('password')}
  type="password"
  autocomplete="new-password"
  {disabled}
  bind:value={draft.password}
/>

<Switch
  checked={draft.requireSsl}
  label={m['server.database.requireSsl']()}
  description={pinned('requireSsl')
    ? m['server.pinned']({ variable: databaseVariable('requireSsl') })
    : m['server.database.requireSsl.hint']()}
  disabled={disabled || pinned('requireSsl')}
  onchange={(value) => (draft.requireSsl = value)}
/>

<SettingField
  label={m['server.database.maxPoolSize']()}
  hint={m['server.database.maxPoolSize.hint']()}
  variable={databaseVariable('maxPoolSize')}
  pinned={pinned('maxPoolSize')}
  inputmode="numeric"
  error={problem(draft.maxPoolSize)}
  {disabled}
  bind:value={draft.maxPoolSize}
/>

<style>
  /* Host and port read as one address, so they sit on one line when they fit. */
  .pair {
    display: grid;
    grid-template-columns: minmax(0, 3fr) minmax(6rem, 1fr);
    gap: var(--space-4);
  }

  @media (max-width: 30rem) {
    .pair {
      grid-template-columns: minmax(0, 1fr);
    }
  }
</style>
