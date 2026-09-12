<script lang="ts">
  import { onDestroy } from 'svelte';

  import { goto } from '$app/navigation';
  import { resolve } from '$app/paths';
  import { page } from '$app/state';
  import FormField from '$features/auth/FormField.svelte';
  import FormFailure from '$features/auth/FormFailure.svelte';
  import SubmitButton from '$features/auth/SubmitButton.svelte';
  import { safeRedirect } from '$features/auth/redirectTarget';
  import { register } from '$features/auth/registration.svelte';
  import { session } from '$features/auth/session.svelte';
  import { createSubmission } from '$features/auth/submission.svelte';
  import { m } from '$shell/i18n';

  /**
   * Create an account.
   *
   * The form asks only what this instance will actually use: the household name
   * appears for the very first account, which is the only time it is read, and
   * the invitation field appears when the policy demands one. A field that is
   * silently ignored is worse than a missing one.
   */
  let { data } = $props();

  let displayName = $state('');
  let email = $state('');
  let password = $state('');
  let householdName = $state('');
  let invitationCode = $state(page.url.searchParams.get('code') ?? '');

  const submission = createSubmission();

  onDestroy(() => submission.dispose());

  const settingUpInstance = $derived(!data.policy.hasAccounts);
  const needsInvitation = $derived(data.policy.requireInvitation && !settingUpInstance);
  const closed = $derived(!data.policy.openRegistration && !settingUpInstance);

  const destination = $derived(safeRedirect(page.url.searchParams.get('next')));
  const loginHref = $derived(`${resolve('/(auth)/login')}?next=${encodeURIComponent(destination)}`);

  async function submit(event: SubmitEvent) {
    event.preventDefault();

    let landedIn: string | null = null;

    const succeeded = await submission.run(async () => {
      const outcome = await register({
        email,
        displayName,
        password,
        householdName: settingUpInstance ? householdName : undefined,
        invitationCode: invitationCode || undefined
      });

      if ('code' in outcome) {
        return outcome;
      }

      landedIn = outcome.householdId;

      // Registering signs you in; asking for the same password again on the
      // next screen would be a pointless second step.
      return session.signIn(email, password);
    });

    if (!succeeded) {
      return;
    }

    // An account with no household is not a dead end — it goes to the screen
    // that offers the two ways out of it.
    await goto(landedIn ? destination : resolve('/(app)/welcome'), { replaceState: true });
  }
</script>

<svelte:head><title>{m['auth.register.title']()}</title></svelte:head>

{#if closed}
  <div class="closed">
    <h1 class="title">{m['auth.register.closed.title']()}</h1>
    <p class="body">{m['auth.register.closed.body']()}</p>
    <a href={loginHref}>{m['auth.register.signIn']()}</a>
  </div>
{:else}
  <form class="form" onsubmit={submit} novalidate>
    <h1 class="title">{m['auth.register.title']()}</h1>

    <FormFailure failure={submission.failure} />

    <FormField
      name="displayName"
      label={m['auth.register.displayName']()}
      autocomplete="name"
      autofocus
      bind:value={displayName}
      {submission}
    />

    <FormField
      name="email"
      label={m['auth.register.email']()}
      type="email"
      autocomplete="username"
      inputmode="email"
      bind:value={email}
      {submission}
    />

    <FormField
      name="password"
      label={m['auth.register.password']()}
      hint={m['auth.register.passwordHint']()}
      type="password"
      autocomplete="new-password"
      bind:value={password}
      {submission}
    />

    {#if settingUpInstance}
      <FormField
        name="householdName"
        label={m['auth.register.householdName']()}
        hint={m['auth.register.householdHint']()}
        bind:value={householdName}
        {submission}
      />
    {/if}

    {#if needsInvitation}
      <FormField
        name="invitationCode"
        label={m['auth.register.invitationCode']()}
        hint={m['auth.register.invitationHint']()}
        bind:value={invitationCode}
        {submission}
      />
    {/if}

    <SubmitButton label={m['auth.register.submit']()} {submission} />

    <p class="alternative">
      {m['auth.register.haveAccount']()}
      <a href={loginHref}>{m['auth.register.signIn']()}</a>
    </p>
  </form>
{/if}

<style>
  .form,
  .closed {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
  }

  .title {
    font-size: var(--text-2xl);
  }

  .body {
    color: var(--text-muted);
  }

  .alternative {
    color: var(--text-muted);
    font-size: var(--text-sm);
    text-align: center;
  }
</style>
