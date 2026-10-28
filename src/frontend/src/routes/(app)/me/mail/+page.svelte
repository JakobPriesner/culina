<script lang="ts">
  import { onDestroy, onMount } from 'svelte';

  import type { AppError } from '$api';
  import { Button, ErrorState, Field, Select, Skeleton, Switch } from '$ds';
  import FormField from '$features/auth/FormField.svelte';
  import FormFailure from '$features/auth/FormFailure.svelte';
  import { createSubmission } from '$features/auth/submission.svelte';
  import {
    draftOf,
    needsPasswordAgain,
    readMailSettings,
    saveMailSettings,
    sendTestMail,
    usualPort,
    type MailDraft,
    type MailSecurity,
    type MailSettings
  } from '$features/server/mail';
  import { explain } from '$shell/explain';
  import { m } from '$shell/i18n';
  import { toaster } from '$shell/toaster.svelte';

  import SettingsSection from '../SettingsSection.svelte';

  /**
   * The mail account sign-up confirmations, sign-in links, password-forgotten links and account notices go out
   * through. Applied on Save with no restart; the test goes through what was saved, so it proves what people get.
   */
  let settings = $state<MailSettings | null>(null);
  let draft = $state<MailDraft | null>(null);
  let failed = $state<AppError | null>(null);
  let testing = $state(false);

  const submission = createSubmission();

  onDestroy(() => submission.dispose());

  async function load() {
    failed = null;

    const result = await readMailSettings();

    if (result.ok) {
      settings = result.value;
      draft = draftOf(result.value);
    } else {
      failed = result.error;
    }
  }

  onMount(load);

  const securities = $derived([
    { value: 'starttls', label: m['mail.security.starttls']() },
    { value: 'tls', label: m['mail.security.tls']() },
    { value: 'none', label: m['mail.security.none']() }
  ]);

  function chooseSecurity(value: string) {
    if (!draft) return;

    // A port still at the old kind's usual one follows the kind; one typed by hand stays.
    if (draft.port === usualPort[draft.security as MailSecurity]) {
      draft.port = usualPort[value as MailSecurity] ?? draft.port;
    }

    draft.security = value;
  }

  const passwordHint = $derived(
    draft && settings && needsPasswordAgain(draft, settings)
      ? m['mail.password.again']()
      : settings?.passwordConfigured
        ? m['mail.password.keep']()
        : m['mail.password.hint']()
  );

  async function save(event: SubmitEvent) {
    event.preventDefault();

    if (!draft) return;

    const sent = draft;
    let saved: MailSettings | null = null;

    const succeeded = await submission.run(async () => {
      const result = await saveMailSettings(sent);

      if (!result.ok) return result.error;

      saved = result.value;

      return null;
    });

    if (succeeded && saved) {
      settings = saved;
      draft = draftOf(saved);
      toaster.show({ message: () => m['mail.saved'](), tone: 'success' });
    }
  }

  async function test() {
    testing = true;

    const failure = await sendTestMail();

    testing = false;

    toaster.show(
      failure
        ? {
            // The server's own words: what the mail server answered is what the administrator has to act on.
            message: () =>
              failure.code === 'mail.send_failed' ? failure.detail : explain(failure),
            tone: 'danger'
          }
        : { message: () => m['mail.test.sent'](), tone: 'success' }
    );
  }
</script>

<svelte:head><title>{m['me.mail']()}</title></svelte:head>

{#if failed}
  <ErrorState
    title={m['mail.failed.title']()}
    body={m['mail.failed.body']()}
    requestIdLabel={m['error.reference']()}
    requestId={failed.requestId}
  >
    {#snippet action()}
      <Button variant="primary" onclick={load}>{m['error.retry']()}</Button>
    {/snippet}
  </ErrorState>
{:else if draft && settings}
  <form class="page" onsubmit={save} novalidate>
    {#if !settings.siteUrl}
      <p class="notice" role="note">{m['mail.siteUrl.missing']()}</p>
    {/if}

    <SettingsSection title={m['mail.account']()} description={m['mail.account.hint']()}>
      <div class="fields">
        <Switch
          checked={draft.enabled}
          label={m['mail.enabled']()}
          description={m['mail.enabled.hint']()}
          onchange={(value) => draft && (draft.enabled = value)}
        />

        <div class="pair">
          <FormField
            name="host"
            label={m['mail.host']()}
            autocomplete="off"
            required={false}
            bind:value={draft.host}
            {submission}
          />
          <FormField
            name="port"
            label={m['mail.port']()}
            autocomplete="off"
            required={false}
            bind:value={draft.port}
            {submission}
          />
        </div>

        <Field
          label={m['mail.security']()}
          hint={m['mail.security.hint']()}
          error={submission.errorFor('security')}
        >
          {#snippet children({ id, describedBy, invalid })}
            <Select
              {id}
              {describedBy}
              {invalid}
              value={draft!.security}
              options={securities}
              onchange={chooseSecurity}
            />
          {/snippet}
        </Field>

        <FormField
          name="username"
          label={m['mail.username']()}
          hint={m['mail.username.hint']()}
          autocomplete="off"
          required={false}
          bind:value={draft.username}
          {submission}
        />

        <FormField
          name="password"
          label={m['mail.password']()}
          hint={passwordHint}
          type="password"
          autocomplete="new-password"
          required={false}
          bind:value={draft.password}
          {submission}
        />
      </div>
    </SettingsSection>

    <SettingsSection title={m['mail.sender']()} description={m['mail.sender.hint']()}>
      <div class="fields">
        <FormField
          name="fromAddress"
          label={m['mail.fromAddress']()}
          type="email"
          inputmode="email"
          autocomplete="off"
          required={false}
          bind:value={draft.fromAddress}
          {submission}
        />
        <FormField
          name="fromName"
          label={m['mail.fromName']()}
          autocomplete="off"
          required={false}
          bind:value={draft.fromName}
          {submission}
        />
      </div>
    </SettingsSection>

    <FormFailure failure={submission.failure} />

    <div class="actions">
      <Button type="submit" variant="primary" loading={submission.inFlight}
        >{m['mail.save']()}</Button
      >
      <Button onclick={test} loading={testing} disabled={!settings.sending || submission.inFlight}>
        {m['mail.test']()}
      </Button>
      <p class="note">
        {settings.sending
          ? m['mail.state.sending']({ address: settings.siteUrl ?? '' })
          : m['mail.state.off']()}
      </p>
    </div>
  </form>
{:else}
  <div class="loading" aria-busy="true">
    {#each { length: 2 } as _, index (index)}
      <div class="placeholder" aria-hidden="true">
        <Skeleton width="10rem" height="1.25rem" />
        <Skeleton width="100%" height="9rem" shape="block" />
      </div>
    {/each}
  </div>
{/if}

<style>
  .page {
    display: flex;
    flex-direction: column;
    gap: var(--layout-section-gap);
  }

  .fields {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
    padding: var(--space-4);
  }

  /* Server and port read as one address, so they sit on one line when they fit. */
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

  .actions {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-2) var(--space-4);
  }

  .note,
  .notice {
    max-width: var(--measure);
    color: var(--text-muted);
    font-size: var(--text-sm);
    line-height: var(--leading-normal);
  }

  .notice {
    padding: var(--space-3) var(--space-4);
    border: 1px solid var(--border);
    border-radius: var(--radius-lg);
    background: var(--surface-raised);
    --field-surface: var(--field-surface-inset);
    color: var(--text);
  }

  .loading,
  .placeholder {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
  }

  .loading {
    gap: var(--layout-section-gap);
  }
</style>
