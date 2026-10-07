<script lang="ts">
  import { Button, Field, RadioGroup, TextInput, type RadioOption } from '$ds';

  import FormFailure from '$features/auth/FormFailure.svelte';
  import { m } from '$shell/i18n';

  import { originOf, tokenPageOf } from './sourceAddress';
  import { sources } from './stores/sources.svelte';
  import type { ConnectedSource } from './types';

  /**
   * Connects another app. The address comes first and alone: the token-page link and the sign-in
   * both need it. Signing in is the default (a token is a task people give up on), but the token
   * stays for SSO-only instances and anyone who won't hand over a password.
   */
  interface Props {
    householdId: string;
    onconnected: (source: ConnectedSource) => void;
  }

  let { householdId, onconnected }: Props = $props();

  type Way = 'signIn' | 'token';

  let address = $state('');
  let way = $state<Way>('signIn');
  let username = $state('');
  let password = $state('');
  let token = $state('');

  /** Where the person would go to make a token, once we know which server. */
  const tokenPage = $derived(tokenPageOf(address));

  const ready = $derived(
    originOf(address) !== null &&
      (way === 'token'
        ? token.trim().length > 0
        : username.trim().length > 0 && password.length > 0)
  );

  const ways: readonly RadioOption[] = $derived([
    {
      value: 'signIn',
      label: m['import.source.waySignIn'](),
      description: m['import.source.waySignInHint']()
    },
    {
      value: 'token',
      label: m['import.source.wayToken'](),
      description: m['import.source.wayTokenHint']()
    }
  ]);

  async function submit(event: SubmitEvent) {
    event.preventDefault();

    if (!ready) {
      return;
    }

    const connected = await sources.connect({
      householdId,
      kind: 'tandoor',
      address: address.trim(),
      ...(way === 'token' ? { token: token.trim() } : { username: username.trim(), password })
    });

    if (connected) {
      // Cleared whatever happens next: the password has done its one job and must not linger on a shared tablet.
      address = '';
      username = '';
      password = '';
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

  <!-- Hidden until an address exists: how to sign in to an unnamed server has no answer. -->
  {#if originOf(address)}
    <Field label={m['import.source.wayLabel']()} group>
      {#snippet children({ id, describedBy })}
        <RadioGroup
          name={id}
          {describedBy}
          options={ways}
          value={way}
          onchange={(chosen) => (way = chosen === 'token' ? 'token' : 'signIn')}
        />
      {/snippet}
    </Field>

    {#if way === 'signIn'}
      <Field label={m['import.source.username']()}>
        {#snippet children({ id, describedBy, invalid })}
          <TextInput {id} {describedBy} {invalid} autocomplete="off" bind:value={username} />
        {/snippet}
      </Field>

      <Field label={m['import.source.password']()} hint={m['import.source.passwordHint']()}>
        {#snippet children({ id, describedBy, invalid })}
          <TextInput
            {id}
            {describedBy}
            {invalid}
            type="password"
            revealLabel={m['field.showPassword']()}
            autocomplete="off"
            bind:value={password}
          />
        {/snippet}
      </Field>
    {:else}
      <Field label={m['import.source.token']()} hint={m['import.source.tokenHint']()}>
        {#snippet children({ id, describedBy, invalid })}
          <TextInput
            {id}
            {describedBy}
            {invalid}
            type="password"
            revealLabel={m['field.showToken']()}
            autocomplete="off"
            bind:value={token}
          />
        {/snippet}
      </Field>

      {#if tokenPage}
        <!-- Built from what they typed, so it goes to *their* server; a new tab keeps the half-filled form. -->
        <p class="helper">
          <!-- eslint-disable-next-line svelte/no-navigation-without-resolve -->
          <a href={tokenPage} rel="noreferrer" target="_blank">
            {m['import.source.openTokenPage']({ where: originOf(address) ?? '' })}
          </a>
        </p>
      {/if}
    {/if}

    <div>
      <Button type="submit" variant="primary" disabled={!ready} loading={sources.connecting}>
        {m['import.source.connect']()}
      </Button>
    </div>
  {/if}
</form>

<style>
  .connect {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
  }

  .helper {
    font-size: var(--text-sm);
  }
</style>
