<script lang="ts">
  import { Button, Field, TextInput } from '$ds';

  import FormFailure from '$features/auth/FormFailure.svelte';
  import { m } from '$shell/i18n';

  import { sources } from './stores/sources.svelte';
  import type { ConnectedSource } from './types';

  /**
   * Pointing this app at another one.
   *
   * Two fields, because two is what it takes and a third would be a third
   * chance to get something wrong. The name is not asked for at all: a
   * connection is named after its host unless somebody has a reason to say
   * otherwise, and almost nobody does.
   *
   * The address is deliberately forgiving — a bare host, a trailing slash, the
   * whole contents of the address bar all mean the same instance — because what
   * people paste is whatever their browser was showing.
   */
  interface Props {
    householdId: string;
    onconnected: (source: ConnectedSource) => void;
  }

  let { householdId, onconnected }: Props = $props();

  let address = $state('');
  let token = $state('');

  const ready = $derived(address.trim().length > 0 && token.trim().length > 0);

  async function submit(event: SubmitEvent) {
    event.preventDefault();

    if (!ready) {
      return;
    }

    const connected = await sources.connect({
      householdId,
      kind: 'tandoor',
      address: address.trim(),
      token: token.trim()
    });

    if (connected) {
      address = '';
      token = '';
      onconnected(connected);
    }
  }
</script>

<form class="connect" onsubmit={submit} novalidate>
  <FormFailure failure={sources.connectError} />

  <Field label={m['import.source.address']()} hint={m['import.source.addressHint']()}>
    {#snippet children({ id, describedBy, invalid })}
      <TextInput
        {id}
        {describedBy}
        {invalid}
        type="url"
        inputmode="url"
        placeholder="https://"
        autocomplete="off"
        bind:value={address}
      />
    {/snippet}
  </Field>

  <Field label={m['import.source.token']()} hint={m['import.source.tokenHint']()}>
    {#snippet children({ id, describedBy, invalid })}
      <!-- A password field: this is a credential, and a kitchen tablet is a
           screen other people stand in front of. -->
      <TextInput
        {id}
        {describedBy}
        {invalid}
        type="password"
        autocomplete="off"
        bind:value={token}
      />
    {/snippet}
  </Field>

  <div>
    <Button type="submit" variant="primary" disabled={!ready} loading={sources.connecting}>
      {m['import.source.connect']()}
    </Button>
  </div>
</form>

<style>
  .connect {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
  }
</style>
