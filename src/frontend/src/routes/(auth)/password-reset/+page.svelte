<script lang="ts">
  import { onDestroy, onMount } from 'svelte';

  import { goto } from '$app/navigation';
  import { resolve } from '$app/paths';
  import { ErrorCodes } from '$api';
  import AuthIntro from '$features/auth/AuthIntro.svelte';
  import FormField from '$features/auth/FormField.svelte';
  import FormFailure from '$features/auth/FormFailure.svelte';
  import CheckInbox from '$features/auth/CheckInbox.svelte';
  import { requestPasswordResetLink, takeLinkFragment } from '$features/auth/mailLinks';
  import SubmitButton from '$features/auth/SubmitButton.svelte';
  import { resetPassword } from '$features/auth/recovery';
  import { session } from '$features/auth/session.svelte';
  import { createSubmission } from '$features/auth/submission.svelte';
  import { m } from '$shell/i18n';

  /**
   * Back in after a forgotten password. With mail, the first step mails a link that comes back here with the address
   * and a code filled in; without, the page says where a code comes from. Success signs straight in with the new
   * password.
   */
  let { data } = $props();

  let email = $state('');
  let code = $state('');
  let password = $state('');

  // A choice made once, from what the page was opened with; data does not change while it is open.
  // svelte-ignore state_referenced_locally
  let mode = $state<'ask' | 'sent' | 'code'>(data.policy.sendsMail ? 'ask' : 'code');

  onMount(() => {
    const link = takeLinkFragment();
    const mailedEmail = link.get('email');
    const mailedCode = link.get('code');

    // Opened from the mail: only the new password is left to type.
    if (mailedEmail && mailedCode) {
      email = mailedEmail;
      code = mailedCode;
      mode = 'code';
    }
  });

  const submission = createSubmission();

  onDestroy(() => submission.dispose());

  async function sendLink(event: SubmitEvent) {
    event.preventDefault();

    if (await submission.run(() => requestPasswordResetLink(email))) {
      mode = 'sent';
    }
  }

  function choose(next: 'ask' | 'code') {
    submission.clear();
    mode = next;
  }

  async function submit(event: SubmitEvent) {
    event.preventDefault();

    const succeeded = await submission.run(async () => {
      const failure = await resetPassword(email, code, password);

      return failure ?? (await session.signIn(email, password));
    });

    if (!succeeded) {
      return;
    }

    await goto(resolve('/(app)'), { replaceState: true });
  }
</script>

<svelte:head><title>{m['auth.reset.title']()}</title></svelte:head>

{#if mode === 'sent'}
  <CheckInbox title={m['auth.reset.sent.title']()} body={m['auth.reset.sent.body']({ email })}>
    <p class="alternative">
      <button class="switch" type="button" onclick={() => choose('code')}>
        {m['auth.reset.haveCode']()}
      </button>
    </p>
  </CheckInbox>
{:else if mode === 'ask'}
  <form class="form" onsubmit={sendLink} novalidate>
    <AuthIntro title={m['auth.reset.title']()} subtitle={m['auth.reset.ask.intro']()} />

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

    <SubmitButton label={m['auth.reset.ask.submit']()} {submission} />

    <p class="alternative">
      <button class="switch" type="button" onclick={() => choose('code')}>
        {m['auth.reset.haveCode']()}
      </button>
    </p>

    <p class="alternative">
      <a href={resolve('/(auth)/login')}>{m['auth.reset.back']()}</a>
    </p>
  </form>
{:else}
  <form class="form" onsubmit={submit} novalidate>
    <AuthIntro
      title={m['auth.reset.title']()}
      subtitle={data.policy.sendsMail ? m['auth.reset.intro.mail']() : m['auth.reset.intro']()}
    />

    <!-- One message for unknown address and wrong, used or expired code: telling them apart reveals registered addresses. -->
    <FormFailure
      failure={submission.failure}
      message={submission.failure?.code === ErrorCodes.invalidRecoveryCode
        ? m['auth.reset.failed']()
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
      name="code"
      label={m['auth.reset.code']()}
      hint={m['auth.reset.codeHint']()}
      autocomplete="one-time-code"
      bind:value={code}
      {submission}
    />

    <FormField
      name="password"
      label={m['auth.reset.password']()}
      hint={m['auth.register.passwordHint']()}
      type="password"
      autocomplete="new-password"
      bind:value={password}
      {submission}
    />

    <SubmitButton label={m['auth.reset.submit']()} {submission} />

    <p class="alternative">
      <a href={resolve('/(auth)/login')}>{m['auth.reset.back']()}</a>
    </p>
  </form>
{/if}

<style>
  .form {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
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
