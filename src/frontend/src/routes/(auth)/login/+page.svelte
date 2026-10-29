<script lang="ts">
  import { onDestroy, onMount } from 'svelte';

  import { goto } from '$app/navigation';
  import { resolve } from '$app/paths';
  import { page } from '$app/state';
  import { ErrorCodes } from '$api';
  import AuthIntro from '$features/auth/AuthIntro.svelte';
  import FormField from '$features/auth/FormField.svelte';
  import ColdStove from '$features/auth/ColdStove.svelte';
  import FamilySecret from '$features/auth/FamilySecret.svelte';
  import FormFailure from '$features/auth/FormFailure.svelte';
  import CheckInbox from '$features/auth/CheckInbox.svelte';
  import { requestSignInLink } from '$features/auth/mailLinks';
  import { readPolicy } from '$features/auth/registration.svelte';
  import SubmitButton from '$features/auth/SubmitButton.svelte';
  import { safeRedirect } from '$features/auth/redirectTarget';
  import { session } from '$features/auth/session.svelte';
  import { createSubmission } from '$features/auth/submission.svelte';
  import { m } from '$shell/i18n';

  /** Sign in and return to where you were headed: the `next` parameter makes deep links survive a sign-in. */
  let email = $state('');
  let password = $state('');

  /** With mail, a sign-in link instead of the password; `sent` once one is on its way. */
  let mode = $state<'password' | 'link' | 'sent'>('password');

  /** Learnt after the form is drawn, so the sign-in screen never waits on it; the link appears when it is known. */
  let sendsMail = $state(false);

  onMount(async () => {
    sendsMail = (await readPolicy()).sendsMail ?? false;
  });

  const submission = createSubmission();

  onDestroy(() => submission.dispose());

  // From the URL, so only a path inside this app is accepted (`safeRedirect`).
  const destination = $derived(safeRedirect(page.url.searchParams.get('next')));

  /** Why the form appeared when it was not the visitor's idea (expired session, followed link); nothing is said to someone who just opened the app. */
  const moment = $derived(
    page.url.searchParams.get('reason') === 'expired'
      ? 'cold'
      : destination !== resolve('/(app)')
        ? 'secret'
        : null
  );

  const heading = $derived(
    moment === 'cold'
      ? {
          title: m['auth.cold.title'](),
          body: m['auth.cold.body'](),
          submit: m['auth.cold.submit']()
        }
      : moment === 'secret'
        ? {
            title: m['auth.secret.title'](),
            body: m['auth.secret.body'](),
            submit: m['auth.secret.submit']()
          }
        : {
            title: m['auth.signIn.title'](),
            body: m['auth.signIn.intro'](),
            submit: m['auth.signIn.submit']()
          }
  );

  // Each typed character lights a flame; the whole burner while submitting.
  const flames = $derived(submission.inFlight ? 9 : password.length);

  const registerHref = $derived(
    `${resolve('/(auth)/register')}?next=${encodeURIComponent(destination)}`
  );

  async function sendLink(event: SubmitEvent) {
    event.preventDefault();

    if (await submission.run(() => requestSignInLink(email))) {
      mode = 'sent';
    }
  }

  function choose(next: 'password' | 'link') {
    submission.clear();
    mode = next;
  }

  async function submit(event: SubmitEvent) {
    event.preventDefault();

    const succeeded = await submission.run(() => session.signIn(email, password));

    if (!succeeded) {
      // The address stays: retyping it punishes a typo in the other field.
      password = '';

      return;
    }

    // Replaced, not pushed, so back does not land on a sign-in form for an existing session.
    await goto(destination, { replace: true });
  }
</script>

<svelte:head><title>{m['auth.signIn.title']()}</title></svelte:head>

{#if mode === 'sent'}
  <CheckInbox title={m['auth.link.sent.title']()} body={m['auth.link.sent.body']({ email })}>
    <p class="alternative">
      <button class="switch" type="button" onclick={() => choose('password')}>
        {m['auth.link.usePassword']()}
      </button>
    </p>
  </CheckInbox>
{:else if mode === 'link'}
  <form class="form" onsubmit={sendLink} novalidate>
    <AuthIntro title={m['auth.link.ask.title']()} subtitle={m['auth.link.ask.intro']()} />

    <FormFailure failure={submission.failure} />

    <FormField
      name="email"
      label={m['auth.signIn.email']()}
      type="email"
      autocomplete="username"
      inputmode="email"
      autofocus
      bind:value={email}
      {submission}
    />

    <SubmitButton label={m['auth.link.ask.submit']()} {submission} />

    <p class="alternative">
      <button class="switch" type="button" onclick={() => choose('password')}>
        {m['auth.link.usePassword']()}
      </button>
    </p>
  </form>
{:else}
  <form class="form" onsubmit={submit} novalidate>
    {#if moment === 'cold'}
      <div class="moment"><ColdStove lit={flames} /></div>
    {:else if moment === 'secret'}
      <div class="moment"><FamilySecret /></div>
    {/if}

    <AuthIntro
      eyebrow={m['auth.signIn.welcome']()}
      title={heading.title}
      subtitle={heading.body}
      long={moment !== null}
    />

    <!-- One message for "no such account" and "wrong password", so the form cannot reveal which addresses are registered. -->
    <FormFailure
      failure={submission.failure}
      message={submission.failure?.code === ErrorCodes.invalidCredentials
        ? m['auth.signIn.failed']()
        : undefined}
    />

    <FormField
      name="email"
      label={m['auth.signIn.email']()}
      type="email"
      autocomplete="username"
      inputmode="email"
      autofocus
      bind:value={email}
      {submission}
    />

    <FormField
      name="password"
      label={m['auth.signIn.password']()}
      type="password"
      autocomplete="current-password"
      bind:value={password}
      {submission}
    />

    <SubmitButton label={heading.submit} {submission} />

    {#if sendsMail}
      <p class="alternative">
        <button class="switch" type="button" onclick={() => choose('link')}>
          {m['auth.link.useLink']()}
        </button>
      </p>
    {/if}

    <p class="alternative">
      <a href={resolve('/(auth)/password-reset')}>{m['auth.signIn.forgot']()}</a>
    </p>

    <p class="alternative">
      {m['auth.signIn.noAccount']()}
      <a href={registerHref}>{m['auth.signIn.register']()}</a>
    </p>
  </form>
{/if}

<style>
  .form {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
  }

  .moment {
    display: grid;
    justify-items: center;
    margin-bottom: var(--space-2);
  }
  .alternative {
    color: var(--text-muted);
    font-size: var(--text-sm);
    text-align: center;
  }

  /* A link's look for a choice that stays on this page. */
  .switch {
    border: 0;
    background: none;
    padding: 0;
    color: var(--accent);
    font: inherit;
    text-decoration: underline;
    cursor: pointer;
  }
</style>
