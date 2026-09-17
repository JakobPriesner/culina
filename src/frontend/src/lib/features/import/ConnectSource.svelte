<script lang="ts">
  import { Button, Field, RadioGroup, TextInput, type RadioOption } from '$ds';

  import FormFailure from '$features/auth/FormFailure.svelte';
  import { m } from '$shell/i18n';

  import { originOf, tokenPageOf } from './sourceAddress';
  import { sources } from './stores/sources.svelte';
  import type { ConnectedSource } from './types';

  /**
   * Pointing this app at another one.
   *
   * The address comes first and alone, because both of the things that follow
   * need it: the link to that server's own token page is built from it, and
   * signing in has somewhere to send the name and password only once it is
   * known. A form that asked for all four at once would be asking for a token
   * before it could offer any help getting one.
   *
   * Signing in is the default. "Make an API token first" is a task somebody has
   * to go away and learn before they can begin, and it is where most attempts
   * to move a recipe library stop — so the way in that uses what people already
   * know is the one that is offered.
   *
   * The token stays, and is not hidden away as an advanced option: an instance
   * where everyone signs in through single sign-on has no password to give, and
   * some people would simply rather not hand one over. Both are good reasons
   * and neither is unusual.
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
      // Cleared whatever happens next: the password has done its one job, and
      // leaving it sitting in a form on a kitchen tablet is the opposite of
      // what "used once and not stored" means.
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

  <!-- Nothing below appears until there is somewhere to send it. Asking how to
       sign in to a server nobody has named yet is a question with no answer. -->
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
            autocomplete="off"
            bind:value={token}
          />
        {/snippet}
      </Field>

      {#if tokenPage}
        <!-- Built from what they typed, so it goes to *their* server. Opened in
             a new tab, because coming back to a half-filled form having lost
             the address is worse than the extra tab. -->
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
